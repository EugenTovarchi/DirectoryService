namespace FileService.Core.Models;

/// <summary>
/// Результат регистрации multipart upload во внешнем хранилище.
/// Срок действия сохраняется вместе с UploadId в локальной сессии.
/// </summary>
public sealed record MultipartUploadInfo(string UploadId, DateTime ExpiresAt);
