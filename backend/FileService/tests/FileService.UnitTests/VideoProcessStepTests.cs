using CSharpFunctionalExtensions;
using FileService.Domain.MediaProcessing;
using FluentAssertions;
using SharedService.SharedKernel;

namespace FileService.UnitTests;

public class VideoProcessStepTests
{
    private const string VALID_NAME = "Initialize";
    private const int VALID_ORDER = 1;
    private const double VALID_PROGRESS = 0;
    [Fact]
    public void CreateVideoProcessStep_Should_Be_Successful()
    {
        // Act
        Result<VideoProcessStep, Error> result = VideoProcessStep.Create(VALID_NAME, VALID_ORDER, VALID_PROGRESS);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().NotBeEmpty();
        result.Value.Name.Should().Be(VALID_NAME);
        result.Value.Order.Should().Be(VALID_ORDER);
        result.Value.Progress.Should().Be(VALID_PROGRESS);
        result.Value.Status.Should().Be(VideoProcessStatus.PENDING);
        result.Value.CreatedAt.Should().NotBeNull();
        result.Value.UpdatedAt.Should().NotBeNull();
        result.Value.StartedAt.Should().BeNull();
        result.Value.CompletedAt.Should().BeNull();
        result.Value.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Start_WhenStatusIsPending_ShouldChangeToRunningAndSetStartedAt()
    {
        // Arrange
        var step = CreateValidStep();

        // Act
        var result = step.Start();

        // Assert
        result.IsSuccess.Should().BeTrue();
        step.Status.Should().Be(VideoProcessStatus.RUNNING);
        step.ErrorMessage.Should().BeNull();
        step.StartedAt.Should().NotBeNull();
    }

    [Fact]
    public void Start_WhenStatusIsRunning_ShouldReturnError()
    {
        var step = CreateValidStep();
        step.Start();

        var result = step.Start();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("step.invalid.status");
        result.Error.Message.Should().Be("Step can started just from PENDING status");
        step.Status.Should().Be(VideoProcessStatus.RUNNING);
    }

    [Fact]
    public void Start_WhenStatusIsSucceeded_ShouldReturnError()
    {
        // Arrange
        var step = CreateValidStep();
        step.Start();
        step.Complete();

        // Act
        var result = step.Start();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("step.invalid.status");
        result.Error.Message.Should().Be("Step can started just from PENDING status");
        step.Status.Should().Be(VideoProcessStatus.SUCCEEDED);
    }

    [Fact]
    public void SetProgress_WhenStatusIsRunning_ShouldUpdateProgress()
    {
        // Arrange
        var step = CreateValidStep();
        step.Start();
        double newProgress = 50;

        // Act
        var result = step.SetProgress(newProgress);

        // Assert
        result.IsSuccess.Should().BeTrue();
        step.Progress.Should().Be(newProgress);
        step.Status.Should().Be(VideoProcessStatus.RUNNING);
    }

    [Fact]
    public void SetProgress_WhenStatusIsNotRunning_ShouldSetStatusToRunningAndUpdateProgress()
    {
        // Arrange
        var step = CreateValidStep();

        // Act
        var result = step.SetProgress(50);

        // Assert
        result.IsSuccess.Should().BeTrue();
        step.Status.Should().Be(VideoProcessStatus.RUNNING);
        step.Progress.Should().Be(50);
    }

    [Fact]
    public void SetProgress_WithValueLessThanZero_ShouldReturnError()
    {
        // Arrange
        var step = CreateValidStep();
        step.Start();

        // Act
        var result = step.SetProgress(-10);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("percent.invalid");
        result.Error.Message.Should().Be("Percent must be between 0 and 100");
    }

    [Fact]
    public void SetProgress_WithValueAbove100_ShouldReturnError()
    {
        // Arrange
        var step = CreateValidStep();
        step.Start();

        // Act
        var result = step.SetProgress(150);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("percent.invalid");
        result.Error.Message.Should().Be("Percent must be between 0 and 100");
    }

    [Fact]
    public void Complete_WhenStatusIsRunning_ShouldChangeToSucceededAndSetCompletedAt()
    {
        // Arrange
        var step = CreateValidStep();
        step.Start();
        var beforeCompletedAt = step.CompletedAt;

        // Act
        var result = step.Complete();

        // Assert
        result.IsSuccess.Should().BeTrue();
        step.Status.Should().Be(VideoProcessStatus.SUCCEEDED);
        step.Progress.Should().Be(100);
        step.CompletedAt.Should().NotBeNull();
        step.CompletedAt.Should().BeAfter(beforeCompletedAt ?? DateTime.MinValue);
    }

    [Fact]
    public void Complete_WhenStatusIsPending_ShouldReturnError()
    {
        // Arrange
        var step = CreateValidStep();

        // Act
        var result = step.Complete();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("step.invalid.status");
        result.Error.Message.Should().Be("Step can complete just from RUNNING status");
        step.Status.Should().Be(VideoProcessStatus.PENDING);
    }

    [Fact]
    public void Complete_WhenStatusIsSucceeded_ShouldReturnError()
    {
        // Arrange
        var step = CreateValidStep();
        step.Start();
        step.Complete();

        // Act
        var result = step.Complete();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("step.invalid.status");
        result.Error.Message.Should().Be("Step can complete just from RUNNING status");
        step.Status.Should().Be(VideoProcessStatus.SUCCEEDED);
    }

    [Fact]
    public void Fail_WhenStatusIsRunning_ShouldChangeToFailedAndSaveErrorMessage()
    {
        // Arrange
        var step = CreateValidStep();
        step.Start();
        string errorMessage = "FFmpeg failed to process video";

        // Act
        var result = step.Fail(errorMessage);

        // Assert
        result.IsSuccess.Should().BeTrue();
        step.Status.Should().Be(VideoProcessStatus.FAILED);
        step.ErrorMessage.Should().Be(errorMessage);
        step.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Fail_WhenErrorMessageIsEmpty_ShouldReturnError()
    {
        // Arrange
        var step = CreateValidStep();
        step.Start();

        // Act
        var result = step.Fail(string.Empty);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("step.error.required");
        result.Error.Message.Should().Be("Error message is required");
        step.Status.Should().Be(VideoProcessStatus.RUNNING);
    }

    [Fact]
    public void Fail_WhenStatusIsPending_ShouldReturnError()
    {
        // Arrange
        var step = CreateValidStep();

        // Act
        var result = step.Fail("Some error");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("step.invalid.status");
        result.Error.Message.Should().Be("Step can fail just from RUNNING status");
        step.Status.Should().Be(VideoProcessStatus.PENDING);
    }

    [Fact]
    public void Reset_WhenStatusIsFailed_ShouldResetToPending()
    {
        // Arrange
        var step = CreateValidStep();
        step.Start();
        step.Fail("Some error");

        // Act
        step.Reset();

        // Assert
        step.Status.Should().Be(VideoProcessStatus.PENDING);
        step.Progress.Should().Be(0);
        step.ErrorMessage.Should().BeNull();
        step.StartedAt.Should().BeNull();
        step.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void Reset_WhenStatusIsRunning_ShouldResetToPending()
    {
        // Arrange
        var step = CreateValidStep();
        step.Start();
        step.SetProgress(50);

        // Act
        step.Reset();

        // Assert
        step.Status.Should().Be(VideoProcessStatus.PENDING);
        step.Progress.Should().Be(0);
        step.ErrorMessage.Should().BeNull();
        step.StartedAt.Should().BeNull();
        step.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void Reset_WhenStatusIsSucceeded_ShouldResetToPending()
    {
        // Arrange
        var step = CreateValidStep();
        step.Start();
        step.Complete();

        // Act
        step.Reset();

        // Assert
        step.Status.Should().Be(VideoProcessStatus.PENDING);
        step.Progress.Should().Be(0);
        step.ErrorMessage.Should().BeNull();
        step.StartedAt.Should().BeNull();
        step.CompletedAt.Should().BeNull();
    }

    private static VideoProcessStep CreateValidStep()
    {
        var result = VideoProcessStep.Create(VALID_NAME, VALID_ORDER, VALID_PROGRESS);
        return result.Value;
    }
}
