using Microsoft.Extensions.Options;

namespace FileService.Infrastructure.S3;

public sealed class S3OptionsValidator : IValidateOptions<S3Options>
{
    public ValidateOptionsResult Validate(string? name, S3Options options)
    {
        List<string> failures = [];

        if (options.UploadUrlExpirationHours <= 0)
            failures.Add("S3Options:UploadUrlExpirationHours must be greater than zero.");

        if (options.DownloadUrlExpirationDays <= 0)
            failures.Add("S3Options:DownloadUrlExpirationDays must be greater than zero.");

        if (options.MaxConcurrentRequests <= 0)
            failures.Add("S3Options:MaxConcurrentRequests must be greater than zero.");

        if (options.MaxChunks <= 0)
            failures.Add("S3Options:MaxChunks must be greater than zero.");

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}