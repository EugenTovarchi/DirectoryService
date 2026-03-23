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

    public bool IsCompleted { get; private set; }

    public IReadOnlyList<VideoProcessStep> Steps => _steps;

    public VideoProcessStep? CurrentStep =>
        _steps.FirstOrDefault(step => step.Status == VideoProcessStatus.RUNNING);

    public int? CurrentStepOder => CurrentStep?.Order;

    public string? CurrentStepName => CurrentStep?.Name;
    public double CurrentStepProgress => CurrentStep?.Progress ?? 0;

    public static Result<VideoProcess, Error> Create(
        StorageKey rawKey)
    {
        if (rawKey is null)
            return Error.Validation("videoProcess.rawKey.invalid", "RawKey is required");

        var process = new VideoProcess
        {
            Id = Guid.NewGuid(),
            RawKey = rawKey,
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

    // Тут как я понял нужно выполнить перезапуск шага ?
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
        if (Status != VideoProcessStatus.PENDING && Status != VideoProcessStatus.RUNNING)
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
    /// <returns>Статус: FAILED, сообщение об ошибке и явл ли ошибка критической.</returns>
    public UnitResult<Error> Fail(string errorMessage)
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
        IsCompleted = false;

        RecalculateTotalProgress();

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

    // Каждый шаг должен начинаться с метода StartStер, который переводит статус в RUNNING.
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

        var currentStepCompleteResult = currentStep.Complete();
        if (currentStepCompleteResult.IsFailure)
            return currentStepCompleteResult.Error;

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

        var currentStepCompleteResult = step.Complete();
        if (currentStepCompleteResult.IsFailure)
            return currentStepCompleteResult.Error;

        RecalculateTotalProgress();

        return UnitResult.Success<Error>();
    }
}