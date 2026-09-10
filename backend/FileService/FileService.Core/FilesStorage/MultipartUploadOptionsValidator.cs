using Microsoft.Extensions.Options;

namespace FileService.Core.FilesStorage;

public sealed class MultipartUploadOptionsValidator : IValidateOptions<MultipartUploadOptions>
{
    public ValidateOptionsResult Validate(string? name, MultipartUploadOptions options)
    {
        List<string> failures = [];

        if (options.SessionExpirationHours <= 0)
            failures.Add("MultipartUploadOptions:SessionExpirationHours must be greater than zero.");

        if (options.OperationTimeoutMinutes <= 0)
            failures.Add("MultipartUploadOptions:OperationTimeoutMinutes must be greater than zero.");

        if (options.CleanupIntervalMinutes <= 0)
            failures.Add("MultipartUploadOptions:CleanupIntervalMinutes must be greater than zero.");

        if (options.CleanupBatchSize <= 0)
            failures.Add("MultipartUploadOptions:CleanupBatchSize must be greater than zero.");

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}