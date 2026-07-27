using SharedService.SharedKernel;

namespace FileService.Core.FilesStorage;

/// <summary>
/// Содержит единое правило классификации ошибок object storage,
/// после которых multipart-операцию безопасно повторить.
/// </summary>
public static class StorageErrorClassifier
{
    public static bool IsRetryable(Error error)
    {
        return error.Code is "network.issue"
            or "internal.server.error"
            or "operation.cancelled"
            or "unknown.error";
    }
}
