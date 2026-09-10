using FileService.Domain;
using FileService.Domain.MediaProcessing;
using FluentAssertions;

namespace FileService.UnitTests;

public class VideoProcessTests
{
    private readonly StorageKey _validRawKey;
    private readonly Guid _videoAssetId;

    public VideoProcessTests()
    {
        var rawKeyResult = StorageKey.Create("test-video.mp4", "raw", "file-service-videos");
        _validRawKey = rawKeyResult.Value;
        _videoAssetId = Guid.NewGuid();
    }

    [Fact]
    public void Create_WithValidRawKey_ShouldReturnSuccess()
    {
        var result = VideoProcess.Create(_videoAssetId, _validRawKey);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().NotBeEmpty();
        result.Value.RawKey.Should().Be(_validRawKey);
        result.Value.Status.Should().Be(VideoProcessStatus.PENDING);
        result.Value.TotalProgress.Should().Be(0);
        result.Value.Steps.Should().HaveCount(6);
        result.Value.Steps.All(s => s.Status == VideoProcessStatus.PENDING).Should().BeTrue();
    }

    [Fact]
    public void Create_WithNullRawKey_ShouldReturnError()
    {
        var result = VideoProcess.Create(_videoAssetId, null!);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("videoProcess.rawKey.invalid");
    }

    [Fact]
    public void Create_ShouldPersistConfiguredRetryLimitAndStableProcessId()
    {
        // Arrange
        const int maxRetries = 5;

        // Act
        var result = VideoProcess.Create(_videoAssetId, _validRawKey, maxRetries);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.MaxRetries.Should().Be(maxRetries);
        result.Value.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_WithNegativeRetryLimit_ShouldReturnError()
    {
        var result = VideoProcess.Create(_videoAssetId, _validRawKey, -1);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("processing.max.retries.invalid");
    }

    [Fact]
    public void RetryCount_ShouldIncrementWhenRetryStarts_NotWhenItIsPlanned()
    {
        // Arrange
        var process = VideoProcess.Create(_videoAssetId, _validRawKey, maxRetries: 3).Value;
        Guid videoProcessId = process.Id;
        VideoProcessStep step = process.Steps[0];
        process.StartStep(step.Order, step.Name);
        process.Fail("Temporary failure", isCritical: false);
        var retryObservations = new List<(
            bool Planned,
            int CountBeforeStart,
            bool Prepared,
            int CountAfterStart,
            bool StepStarted,
            bool Failed,
            Guid VideoProcessId)>();

        // Act
        for (int retry = 1; retry <= 3; retry++)
        {
            bool planned = process.PlannedRetry(DateTime.UtcNow.AddMinutes(retry)).IsSuccess;
            int countBeforeStart = process.RetryCount;
            bool prepared = process.PrepareForRetry().IsSuccess;
            int countAfterStart = process.RetryCount;
            bool stepStarted = process.StartStep(step.Order, step.Name).IsSuccess;
            bool failed = process.Fail("Temporary failure", isCritical: false).IsSuccess;
            retryObservations.Add((
                planned,
                countBeforeStart,
                prepared,
                countAfterStart,
                stepStarted,
                failed,
                process.Id));
        }

        // Assert
        for (int index = 0; index < retryObservations.Count; index++)
        {
            var observation = retryObservations[index];
            observation.Planned.Should().BeTrue();
            observation.CountBeforeStart.Should().Be(index);
            observation.Prepared.Should().BeTrue();
            observation.CountAfterStart.Should().Be(index + 1);
            observation.StepStarted.Should().BeTrue();
            observation.Failed.Should().BeTrue();
            observation.VideoProcessId.Should().Be(videoProcessId);
        }

        process.CanRetry().Should().BeFalse();
        process.Id.Should().Be(videoProcessId);
    }

    [Fact]
    public void MarkAsPermanentlyFailed_ShouldPreserveVideoProcessId()
    {
        // Arrange
        var process = VideoProcess.Create(_videoAssetId, _validRawKey).Value;
        Guid videoProcessId = process.Id;

        // Act
        var result = process.MarkAsPermanentlyFailed("Non-retryable failure");

        // Assert
        result.IsSuccess.Should().BeTrue();
        process.Id.Should().Be(videoProcessId);
        process.Status.Should().Be(VideoProcessStatus.FAILED);
        process.IsCriticalError.Should().BeTrue();
    }

    [Fact]
    public void PrepareForRetry_ShouldResetAllStepsBecauseProcessingContextIsEphemeral()
    {
        var process = VideoProcess.Create(_videoAssetId, _validRawKey, maxRetries: 3).Value;
        VideoProcessStep firstStep = process.Steps[0];
        VideoProcessStep secondStep = process.Steps[1];

        process.StartStep(firstStep.Order, firstStep.Name).IsSuccess.Should().BeTrue();
        process.CompleteStep(firstStep.Order).IsSuccess.Should().BeTrue();
        process.StartStep(secondStep.Order, secondStep.Name).IsSuccess.Should().BeTrue();
        process.Fail("Temporary failure", isCritical: false).IsSuccess.Should().BeTrue();

        process.PrepareForRetry().IsSuccess.Should().BeTrue();

        process.Steps.Should().OnlyContain(step => step.Status == VideoProcessStatus.PENDING);
        process.Status.Should().Be(VideoProcessStatus.PENDING);
        process.RetryCount.Should().Be(1);
    }

    [Fact]
    public void Create_ShouldInitializeStepsWithUniqueOrders()
    {
        var result = VideoProcess.Create(_videoAssetId, _validRawKey).Value;
        var steps = result.Steps;

        var orders = steps.Select(s => s.Order).ToList();
        orders.Should().OnlyHaveUniqueItems();
        orders.Should().BeInAscendingOrder();
    }

    [Fact]
    public void HappyPath_PrepareForExecution_StartAllSteps_CompleteAllSteps_FinishProcessing_ShouldSucceed()
    {
        var processResult = VideoProcess.Create(_videoAssetId, _validRawKey).Value;
        var process = processResult;

        process.PrepareForExecution().IsSuccess.Should().BeTrue();

        foreach (var step in process.Steps)
        {
            process.StartStep(step.Order, step.Name).IsSuccess.Should().BeTrue();
            process.CompleteStep(step.Order).IsSuccess.Should().BeTrue();
        }

        var finishResult = process.FinishProcessing();

        finishResult.IsSuccess.Should().BeTrue();
        process.Status.Should().Be(VideoProcessStatus.SUCCEEDED);
        process.IsCompleted.Should().BeTrue();
        process.TotalProgress.Should().Be(100);
    }

    [Fact]
    public void PrepareForExecution_WhenStatusIsFailed_ShouldResetOnlyFailedAndSubsequentSteps()
    {
        var process = VideoProcess.Create(_videoAssetId, _validRawKey).Value;
        process.PrepareForExecution().IsSuccess.Should().BeTrue();

        var step1 = process.Steps[0];
        var step2 = process.Steps[1];

        process.StartStep(step1.Order, step1.Name).IsSuccess.Should().BeTrue();
        process.CompleteStep(step1.Order).IsSuccess.Should().BeTrue();

        process.StartStep(step2.Order, step2.Name).IsSuccess.Should().BeTrue();
        process.CompleteStep(step2.Order).IsSuccess.Should().BeTrue();

        var step3 = process.Steps[2];
        process.StartStep(step3.Order, step3.Name).IsSuccess.Should().BeTrue();
        process.Fail("Processing error").IsSuccess.Should().BeTrue();

        process.Status.Should().Be(VideoProcessStatus.FAILED);
        step1.Status.Should().Be(VideoProcessStatus.SUCCEEDED);
        step2.Status.Should().Be(VideoProcessStatus.SUCCEEDED);
        step3.Status.Should().Be(VideoProcessStatus.FAILED);

        var prepareResult = process.PrepareForExecution();

        prepareResult.IsSuccess.Should().BeTrue();
        process.Status.Should().Be(VideoProcessStatus.PENDING);
        process.ErrorMessage.Should().BeNull();

        step1.Status.Should().Be(VideoProcessStatus.SUCCEEDED);
        step2.Status.Should().Be(VideoProcessStatus.SUCCEEDED);

        step3.Status.Should().Be(VideoProcessStatus.PENDING);
        process.Steps.Skip(3).All(s => s.Status == VideoProcessStatus.PENDING).Should().BeTrue();
    }

    [Fact]
    public void PrepareForExecution_WhenStatusIsCanceled_ShouldReturnError()
    {
        var process = VideoProcess.Create(_videoAssetId, _validRawKey).Value;
        process.PrepareForExecution().IsSuccess.Should().BeTrue();

        var step = process.Steps[0];
        process.StartStep(step.Order, step.Name).IsSuccess.Should().BeTrue();
        process.Cancel("Cancelled by user").IsSuccess.Should().BeTrue();
        process.Status.Should().Be(VideoProcessStatus.CANCELED);

        var prepareResult = process.PrepareForExecution();

        prepareResult.IsFailure.Should().BeTrue();
        prepareResult.Error.Code.Should().Be("processing.invalid.status");
        process.Status.Should().Be(VideoProcessStatus.CANCELED);
    }

    [Fact]
    public void Fail_WhenStatusIsRunning_ShouldFailCurrentStepAndProcess()
    {
        var process = VideoProcess.Create(_videoAssetId, _validRawKey).Value;

        var step = process.Steps[0];
        process.StartStep(step.Order, step.Name).IsSuccess.Should().BeTrue();
        process.CurrentStep.Should().NotBeNull();

        var failResult = process.Fail("FFmpeg crashed");

        failResult.IsSuccess.Should().BeTrue();
        process.Status.Should().Be(VideoProcessStatus.FAILED);
        process.ErrorMessage.Should().Be("FFmpeg crashed");
        process.CurrentStep.Should().BeNull();
    }

    [Fact]
    public void Fail_WhenStatusIsPending_ShouldFailProcessWithoutCurrentStep()
    {
        var process = VideoProcess.Create(_videoAssetId, _validRawKey).Value;

        var failResult = process.Fail("Some error");

        failResult.IsSuccess.Should().BeTrue();
        process.Status.Should().Be(VideoProcessStatus.FAILED);
        process.ErrorMessage.Should().Be("Some error");
    }

    [Fact]
    public void Fail_WithEmptyErrorMessage_ShouldReturnError()
    {
        var process = VideoProcess.Create(_videoAssetId, _validRawKey).Value;
        process.PrepareForExecution().IsSuccess.Should().BeTrue();

        var step = process.Steps[0];
        process.StartStep(step.Order, step.Name).IsSuccess.Should().BeTrue();

        var failResult = process.Fail(string.Empty);

        failResult.IsFailure.Should().BeTrue();
        failResult.Error.Code.Should().Be("processing.error.required");
        process.Status.Should().Be(VideoProcessStatus.RUNNING);
    }

    [Fact]
    public void TotalProgress_ShouldBe10AfterSecondStep()
    {
        var process = VideoProcess.Create(_videoAssetId, _validRawKey).Value;
        process.PrepareForExecution().IsSuccess.Should().BeTrue();

        var step1 = process.Steps.First(s => string.Equals(s.Name, StepNames.Initialize, StringComparison.OrdinalIgnoreCase));
        process.StartStep(step1.Order, step1.Name).IsSuccess.Should().BeTrue();
        process.CompleteStep(step1.Order).IsSuccess.Should().BeTrue();

        process.TotalProgress.Should().Be(0);

        var step2 = process.Steps.First(s => string.Equals(s.Name, StepNames.ExtractMetadata, StringComparison.OrdinalIgnoreCase));
        process.StartStep(step2.Order, step2.Name).IsSuccess.Should().BeTrue();
        process.CompleteStep(step2.Order).IsSuccess.Should().BeTrue();

        process.TotalProgress.Should().Be(10);
    }

    [Fact]
    public void TotalProgress_ShouldBe100WhenAllStepsCompleted()
    {
        var process = VideoProcess.Create(_videoAssetId, _validRawKey).Value;
        process.PrepareForExecution().IsSuccess.Should().BeTrue();

        foreach (var step in process.Steps)
        {
            process.StartStep(step.Order, step.Name).IsSuccess.Should().BeTrue();
            process.CompleteStep(step.Order).IsSuccess.Should().BeTrue();
        }

        process.TotalProgress.Should().Be(100);
    }

    [Fact]
    public void SetHlsKey_WhenUploadStepIsRunning_ShouldSucceed()
    {
        var process = VideoProcess.Create(_videoAssetId, _validRawKey).Value;
        foreach (VideoProcessStep step in process.Steps.TakeWhile(step => step.Name != StepNames.UploadHls))
        {
            process.StartStep(step.Order, step.Name).IsSuccess.Should().BeTrue();
            process.CompleteStep(step.Order).IsSuccess.Should().BeTrue();
        }

        VideoProcessStep uploadStep = process.Steps.First(step => step.Name == StepNames.UploadHls);
        process.StartStep(uploadStep.Order, uploadStep.Name).IsSuccess.Should().BeTrue();
        StorageKey hlsKey = StorageKey.Create("master.m3u8", "hls/video", "file-service-videos").Value;

        process.SetHlsKey(hlsKey).IsSuccess.Should().BeTrue();
        process.HlsKey.Should().Be(hlsKey);
    }

    [Fact]
    public void StartStep_WhenOrderAndNameDoNotMatch_ShouldReturnError()
    {
        var process = VideoProcess.Create(_videoAssetId, _validRawKey).Value;
        process.PrepareForExecution().IsSuccess.Should().BeTrue();

        var result = process.StartStep(99, "NonExistentStep");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("step.not.found");
    }

    [Fact]
    public void CompleteStep_WhenStepIsNotStarted_ShouldReturnError()
    {
        var process = VideoProcess.Create(_videoAssetId, _validRawKey).Value;

        var step = process.Steps[0];
        var result = process.CompleteStep(step.Order);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("processing.invalid.status");
    }

    [Fact]
    public void FinishProcessing_WhenNotAllStepsCompleted_ShouldReturnError()
    {
        var process = VideoProcess.Create(_videoAssetId, _validRawKey).Value;
        process.PrepareForExecution().IsSuccess.Should().BeTrue();

        var step = process.Steps[0];
        process.StartStep(step.Order, step.Name).IsSuccess.Should().BeTrue();
        process.CompleteStep(step.Order).IsSuccess.Should().BeTrue();

        var finishResult = process.FinishProcessing();

        finishResult.IsFailure.Should().BeTrue();
        finishResult.Error.Code.Should().Be("processing.incomplete.steps");
        process.Status.Should().Be(VideoProcessStatus.RUNNING);
    }
}
