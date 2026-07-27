using FileService.Core.FilesStorage;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace FileService.UnitTests;

public sealed class MultipartUploadOptionsTests
{
    [Fact]
    public void Validate_WithDefaultValues_ShouldSucceed()
    {
        ValidateOptionsResult result =
            new MultipartUploadOptionsValidator().Validate(null, new MultipartUploadOptions());

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithNonPositiveValues_ShouldReportEveryInvalidSetting()
    {
        var options = new MultipartUploadOptions
        {
            SessionExpirationHours = 0,
            OperationTimeoutMinutes = 0,
            CleanupIntervalMinutes = 0,
            CleanupBatchSize = 0,
        };

        ValidateOptionsResult result = new MultipartUploadOptionsValidator().Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().HaveCount(4);
    }
}
