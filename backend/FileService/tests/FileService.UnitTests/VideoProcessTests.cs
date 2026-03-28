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
    public void Fail_WhenStatusIsNotRunning_ShouldReturnError()
    {
        var process = VideoProcess.Create(_videoAssetId, _validRawKey).Value;

        var failResult = process.Fail("Some error");

        failResult.IsFailure.Should().BeTrue();
        failResult.Error.Code.Should().Be("processing.invalid.status");
        process.Status.Should().Be(VideoProcessStatus.PENDING);
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

        var step1 = process.Steps.First(s => s.Name == StepNames.Initialize);
        process.StartStep(step1.Order, step1.Name).IsSuccess.Should().BeTrue();
        process.CompleteStep(step1.Order).IsSuccess.Should().BeTrue();

        process.TotalProgress.Should().Be(0);

        var step2 = process.Steps.First(s => s.Name == StepNames.ExtractMetadata);
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

    private static VideoProcess CreateValidProcess()
    {
        var rawKeyResult = StorageKey.Create("test-video.mp4", "raw", "file-service-videos");
        var videoAssetId = Guid.NewGuid();
        var processResult = VideoProcess.Create(videoAssetId, rawKeyResult.Value);
        return processResult.Value;
    }
}