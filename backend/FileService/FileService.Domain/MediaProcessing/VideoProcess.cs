using CSharpFunctionalExtensions;
using FileService.Domain.MediaProcessing.VO;
using SharedService.SharedKernel;

namespace FileService.Domain.MediaProcessing;

public sealed class VideoProcess
{
    private static readonly (string Name, double Progress)[] _stepsProgress =
    [
        (StepNames.Initialize, 0),
        (StepNames.ExtractMetadata, 10),
        (StepNames.GenerateHls, 60),
        (StepNames.UploadHls, 15),
        (StepNames.GeneratePreview, 10),
        (StepNames.Cleanup, 5)
    ];

    private static readonly Dictionary<string, double> _stepWeights =
        _stepsProgress.ToDictionary(sp => sp.Name, sp => sp.Progress);

    // Этапы обработки видео.
    private readonly List<VideoProcessStep> _steps = [];

    // For EFCore.
    private VideoProcess() { }

    public Guid Id { get; private set; }
    public Guid VideoAssetId { get; private set; }

    // Исходный файл в S3 хранилище.
    public StorageKey RawKey { get; private set; }

    // Результат обработки файла(видео): playlist.m3u8.
    public StorageKey? HlsKey { get; private set; }

    public VideoProcessStatus Status { get; private set; }

    public double TotalProgress { get; private set; }

    // Информация о видео(длительность, разрешение и тд.)
    public VideoMetadata? MetaData { get; private set; }
    public string? ErrorMessage { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? UpdatedAt { get; private set; }

    public DateTime? NextRetryAt { get; private set; }

    public bool IsCompleted { get; private set; }

    public bool IsCriticalError { get; private set; }

    public int MaxRetries { get; private set; } = 3;
    public int RetryCount { get; private set; }

    public IReadOnlyList<VideoProcessStep> Steps => _steps;

    public VideoProcessStep? CurrentStep =>
        _steps.FirstOrDefault(step => step.Status == VideoProcessStatus.RUNNING);

    public int? CurrentStepOder => CurrentStep?.Order;

    public string? CurrentStepName => CurrentStep?.Name;
    public double CurrentStepProgress => CurrentStep?.Progress ?? 0;

    public bool CanRetry() => RetryCount < MaxRetries && !IsCriticalError;

    public static Result<VideoProcess, Error> Create(Guid videoAssetId, StorageKey rawKey)
    {
        if (rawKey is null)
            return Error.Validation("videoProcess.rawKey.invalid", "RawKey is required");

        if (videoAssetId == Guid.Empty)
            return Error.Validation("videoAssetId.is.empty", "videoAssetId is required");

        var process = new VideoProcess
        {
            Id = Guid.NewGuid(),
            RawKey = rawKey,
            VideoAssetId = videoAssetId,
            Status = VideoProcessStatus.PENDING,
            TotalProgress = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        process.InitializeSteps();

        return process;
    }

    private void InitializeSteps()
    {
        int order = 1;

        foreach ((string name, double progress) in _stepsProgress)
        {
            var stepResult = VideoProcessStep.Create(name, order++, progress);
            if (stepResult.IsFailure)
                return;

            _steps.Add(stepResult.Value);
        }
    }

    public bool CheckIsCompleted() => Status is VideoProcessStatus.SUCCEEDED or VideoProcessStatus.CANCELED
        or VideoProcessStatus.FAILED;

    private UnitResult<Error> ReportStepProgress(double progress)
    {
        var currentStep = CurrentStep;
        if (currentStep is null)
        {
            return Error.Validation("processing.no.active.step",
                "No active step to report progress");
        }

        if (progress < 0 || progress > 100)
        {
            return Error.Validation("progress.invalid",
                "Progress must be between 0 and 100");
        }

        var setProgressResult = currentStep.SetProgress(progress);
        if (setProgressResult.IsFailure)
            return setProgressResult.Error;

        RecalculateTotalProgress();

        UpdatedAt = DateTime.UtcNow;

        return UnitResult.Success<Error>();
    }

    private void RecalculateTotalProgress()
    {
        double totalProgress = 0;
        bool allStepsCompleted = true;

        foreach (var step in _steps.OrderBy(s => s.Order))
        {
            double stepWeight = _stepWeights[step.Name];

            if (step.Status == VideoProcessStatus.SUCCEEDED)
            {
                totalProgress += stepWeight;
            }
            else if (step.Status == VideoProcessStatus.RUNNING)
            {
                totalProgress += (step.Progress / 100) * stepWeight;
                allStepsCompleted = false;
                break;
            }
            else
            {
                allStepsCompleted = false;
                break;
            }
        }

        if (allStepsCompleted && _steps.All(s => s.Status == VideoProcessStatus.SUCCEEDED))
        {
            TotalProgress = 100;
        }
        else
        {
            TotalProgress = totalProgress;
        }
    }

    public UnitResult<Error> PrepareForExecution()
    {
        switch (Status)
        {
            case VideoProcessStatus.CANCELED:
                return Error.Validation("processing.invalid.status",
                    "Cannot resume canceled process");

            case VideoProcessStatus.SUCCEEDED:
                return Error.Validation("processing.invalid.status",
                    "Process already completed");

            case VideoProcessStatus.RUNNING or VideoProcessStatus.FAILED:
                {
                    bool foundFailedOrRunning = false;

                    foreach (var step in _steps.OrderBy(s => s.Order))
                    {
                        if (!foundFailedOrRunning && step.Status == VideoProcessStatus.SUCCEEDED)
                        {
                            continue;
                        }

                        foundFailedOrRunning = true;
                        step.Reset();
                    }

                    Status = VideoProcessStatus.PENDING;
                    UpdatedAt = DateTime.UtcNow;
                    ErrorMessage = null;
                    RecalculateTotalProgress();
                    break;
                }
        }

        return UnitResult.Success<Error>();
    }

    public UnitResult<Error> StartStep(int order, string name)
    {
        if (Status is not(VideoProcessStatus.PENDING or VideoProcessStatus.RUNNING))
        {
            return Error.Validation("invalid.process.status",
                $"Process must be in PENDING or RUNNING status, current: {Status}");
        }

        VideoProcessStep? stepToStart = _steps.FirstOrDefault(s => s.Order == order && s.Name == name);
        if (stepToStart is null)
            return Error.Validation("step.not.found", "Step not found");

        if (stepToStart.Status != VideoProcessStatus.PENDING)
        {
            return Error.Validation("step.invalid.status",
                $"Step must be PENDING to start, current: {stepToStart.Status}");
        }

        if (Status == VideoProcessStatus.PENDING)
            Status = VideoProcessStatus.RUNNING;

        UpdatedAt = DateTime.UtcNow;

        stepToStart.Start();
        RecalculateTotalProgress();

        return UnitResult.Success<Error>();
    }

    public Result<VideoProcessStep?, Error> ProcessNextStep()
    {
        if (Status != VideoProcessStatus.RUNNING)
        {
            return Error.Failure("processing.invalid.status",
                $"Cannot process the next step when status is {Status}");
        }

        VideoProcessStep? currentStep = CurrentStep;
        if (currentStep != null)
            return currentStep;

        VideoProcessStep? nextStep = _steps.OrderBy(step => step.Order)
            .FirstOrDefault(step => step.Status == VideoProcessStatus.PENDING);

        if (nextStep == null)
        {
            FinishProcessing();

            return Result.Success<VideoProcessStep?, Error>(null);
        }

        UnitResult<Error> startResult = nextStep.Start();
        if (startResult.IsFailure)
            return startResult.Error;

        return nextStep;
    }

    public UnitResult<Error> CompleteStep(int order)
    {
        if (Status != VideoProcessStatus.RUNNING)
        {
            return Error.Failure("processing.invalid.status",
                $"Cannot process the  step when status is {Status}");
        }

        VideoProcessStep? stepToComplete = _steps.FirstOrDefault(s => s.Order == order);
        if (stepToComplete is null)
            return Error.Validation("step.null", "No active step to complete");

        UnitResult<Error> completeResult = stepToComplete.Complete();
        if (completeResult.IsFailure)
            return completeResult.Error;

        UpdatedAt = DateTime.UtcNow;

        RecalculateTotalProgress();

        return UnitResult.Success<Error>();
    }

    /// <summary>
    /// Вызываем если ffmpeg crashed.
    /// Status => FAILED.
    /// </summary>
    /// <param name="errorMessage">Текст ошибки, почему не обработался файл.</param>
    public UnitResult<Error> Cancel(string errorMessage)
    {
        if (Status != VideoProcessStatus.RUNNING)
        {
            return Error.Failure("processing.invalid.status",
                $"Cannot process the  step when status is {Status}");
        }

        VideoProcessStep? currentStep = CurrentStep;
        if (currentStep is null)
            return Error.Validation("processing.no.active.step", "No active step to cancel");

        Status = VideoProcessStatus.CANCELED;
        ErrorMessage = errorMessage;
        UpdatedAt = DateTime.UtcNow;

        return UnitResult.Success<Error>();
    }

    /// <summary>
    /// Убиваем весь процесс.
    /// </summary>
    /// <param name="errorMessage">Сообщение ошибки.</param>
    /// <param name="isCritical">Является ли ошибка критической.</param>
    /// <returns>Статус: FAILED, сообщение об ошибке и явл ли ошибка критической.</returns>
    public UnitResult<Error> Fail(string errorMessage, bool isCritical = false)
    {
        if (Status != VideoProcessStatus.RUNNING)
        {
            return Error.Failure("processing.invalid.status",
                $"Cannot process the  step when status is {Status}");
        }

        if (string.IsNullOrWhiteSpace(errorMessage))
            return Error.Validation("processing.error.required", "Error message is required");

        var currentStep = CurrentStep;
        if (currentStep != null)
        {
            var failStepResult = currentStep.Fail(errorMessage);
            if (failStepResult.IsFailure)
                return failStepResult.Error;
        }

        Status = VideoProcessStatus.FAILED;
        ErrorMessage = errorMessage;
        IsCriticalError = isCritical;
        UpdatedAt = DateTime.UtcNow;

        return UnitResult.Success<Error>();
    }

    // Сбрасываем обработку до начального состояния.
    public UnitResult<Error> Reset()
    {
        if (Status != VideoProcessStatus.FAILED)
            return Error.Validation("processing.invalid.status", "Can reset just when status is FAILED");

        foreach (VideoProcessStep step in _steps)
        {
            step.Reset();
        }

        Status = VideoProcessStatus.PENDING;
        UpdatedAt = DateTime.UtcNow;
        ErrorMessage = null;
        IsCriticalError = false;
        IsCompleted = false;

        RecalculateTotalProgress();

        return UnitResult.Success<Error>();
    }

    public UnitResult<Error> PlannedRetry(DateTime nextRetryAt)
    {
        if (Status != VideoProcessStatus.FAILED)
        {
            return Error.Validation("processing.invalid.status",
                $"Cannot retry the step when status is {Status}");
        }

        if (IsCompleted)
        {
            return Error.Validation("processing.retry.critical",
                $"Cannot retry critical failure");
        }

        if (RetryCount >= MaxRetries)
        {
            return Error.Validation("invalid.retry.count",
                $"Max retries exceeded");
        }

        RetryCount++;
        NextRetryAt = nextRetryAt;

        return UnitResult.Success<Error>();
    }

    /// <summary>
    /// Вызываем когда все шаги выполнены корректно.
    /// </summary>
    /// <returns>Статус, когда выполнилось, прогресс в %.</returns>
    public UnitResult<Error> FinishProcessing()
    {
        bool allStepsCompleted = _steps.All(step => step.Status == VideoProcessStatus.SUCCEEDED);
        if (!allStepsCompleted)
        {
            return Error.Validation("processing.incomplete.steps",
                "Cant complete processing when not all steps a SUCCEEDED");
        }

        Status = VideoProcessStatus.SUCCEEDED;
        UpdatedAt = DateTime.UtcNow;
        IsCompleted = true;
        TotalProgress = 100;

        return UnitResult.Success<Error>();
    }

    public UnitResult<Error> SetMetadata(VideoMetadata metadata)
    {
        if (Status != VideoProcessStatus.RUNNING)
        {
            return Error.Failure("processing.invalid.status",
                $"Cannot process the next step when status is {Status}");
        }

        var currentStep = _steps.FirstOrDefault(s => s.Name == StepNames.ExtractMetadata);
        if (currentStep is null)
            return Error.NotFound("step.not.found", "Not found extract metadata step");

        MetaData = metadata;
        UpdatedAt = DateTime.UtcNow;

        RecalculateTotalProgress();

        return UnitResult.Success<Error>();
    }

    public UnitResult<Error> SetHlsKey(StorageKey hlsKey)
    {
        if (Status != VideoProcessStatus.RUNNING)
            return Error.Failure("processing.invalid.status", $"Status is {Status}");

        var step = _steps.FirstOrDefault(s => s.Name == StepNames.GenerateHls);
        if (step?.Status != VideoProcessStatus.RUNNING)
            return Error.Validation("step.invalid.status", "GenerateHls step is not running");

        HlsKey = hlsKey;
        UpdatedAt = DateTime.UtcNow;

        RecalculateTotalProgress();

        return UnitResult.Success<Error>();
    }

    /// <summary>
    /// Возвращает задержку для следующей попытки.
    /// </summary>
    public TimeSpan GetNextRetryDelay()
    {
        return RetryCount switch
        {
            0 => TimeSpan.FromMinutes(1),
            1 => TimeSpan.FromMinutes(2),
            2 => TimeSpan.FromMinutes(4),
            _ => TimeSpan.FromMinutes(8)
        };
    }

    /// <summary>
    /// Помечаем процесс как окончательно проваленный.
    /// </summary>
    /// <param name="error">Описание критической ошибки.</param>
    /// <returns>Процесс обработки нужно начинать с начала.</returns>
    public UnitResult<Error> MarkAsPermanentlyFailed(string error)
    {
        IsCompleted = true;
        IsCriticalError = true;
        ErrorMessage = $"Permanent failure: {error}";
        UpdatedAt = DateTime.UtcNow;
        Status = VideoProcessStatus.FAILED;

        return UnitResult.Success<Error>();
    }

    /// <summary>
    /// Готовимся к повторной попытке.
    /// </summary>
    public UnitResult<Error> PrepareForRetry()
    {
        if (!CanRetry())
            return Error.Validation("retry.not.allowed", "Cannot retry this process");

        // Тут сбрасываем статус текущего шага
        var currentStep = CurrentStep ?? _steps.FirstOrDefault(s => s.Status == VideoProcessStatus.FAILED);
        currentStep?.Reset();

        Status = VideoProcessStatus.PENDING;
        UpdatedAt = DateTime.UtcNow;
        ErrorMessage = null;

        return UnitResult.Success<Error>();
    }
}