using FileService.Domain;
using FileService.VideoProcessing;
using FileService.VideoProcessing.Pipeline;
using FileService.VideoProcessing.Pipeline.Options;
using FluentAssertions;
using Microsoft.Extensions.Options;
using SharedService.SharedKernel;

namespace FileService.UnitTests;

public class VideoProcessingOptionsTests
{
    [Fact]
    public void PreviewOptionsValidator_WhenMinimumExceedsMaximum_ShouldFail()
    {
        var options = new PreviewOptions
        {
            MinPreviewCount = 5,
            MaxPreviewCount = 3,
        };

        var result = new PreviewOptionsValidator().Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(failure => failure.Contains("MaxPreviewCount", StringComparison.Ordinal));
    }

    [Fact]
    public void VideoProcessingOptionsValidator_WhenPathsAndLimitsAreValid_ShouldSucceed()
    {
        var result = new VideoProcessingOptionsValidator().Validate(null, new VideoProcessingOptions());

        result.Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(1, 120)]
    [InlineData(2, 240)]
    [InlineData(3, 480)]
    [InlineData(10, 480)]
    public void ConfiguredPolicy_ShouldUseCappedExponentialBackoff(int retryCount, int expectedSeconds)
    {
        var options = Options.Create(new VideoProcessingOptions { RetryDelaySeconds = 60 });
        var policy = new ConfiguredVideoProcessingPolicy(options);

        policy.GetRetryDelay(retryCount).Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Fact]
    public void ErrorClassifier_NetworkFailure_ShouldBeTransient()
    {
        var classifier = new ProcessingErrorClassifier();

        classifier.IsCritical(FileErrors.NetworkIssue()).Should().BeFalse();
    }

    [Fact]
    public void ErrorClassifier_InvalidFfprobeOutput_ShouldBePermanent()
    {
        var classifier = new ProcessingErrorClassifier();

        classifier.IsCritical(FileErrors.InvalidFfprobeOutput("invalid json")).Should().BeTrue();
    }

    [Fact]
    public void ErrorClassifier_ValidationFailure_ShouldBePermanent()
    {
        var classifier = new ProcessingErrorClassifier();

        classifier.IsCritical(Error.Validation("video.invalid", "Invalid video")).Should().BeTrue();
    }
}
