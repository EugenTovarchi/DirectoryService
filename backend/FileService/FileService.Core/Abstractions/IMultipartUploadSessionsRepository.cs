using CSharpFunctionalExtensions;
using FileService.Domain.Uploads;
using SharedService.SharedKernel;

namespace FileService.Core.Abstractions;

/// <summary>
/// Хранилище технических multipart-сессий. Методы TryBegin* атомарно
/// изменяют состояние и определяют, какой экземпляр сервиса выполнит операцию в S3.
/// </summary>
public interface IMultipartUploadSessionsRepository
{
    Result<Guid, Error> Add(MultipartUploadSession session);

    Task<Result<MultipartUploadSession, Error>> GetByMediaAssetIdAsync(
        Guid mediaAssetId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ищет ранее созданную сессию для безопасного повтора start-запроса.
    /// </summary>
    Task<MultipartUploadSession?> FindByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Пытается получить право на завершение загрузки.
    /// Возвращает false, если право уже получил другой экземпляр сервиса.
    /// </summary>
    Task<Result<bool, Error>> TryBeginCompletionAsync(
        Guid mediaAssetId,
        string uploadId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Пытается получить право на отмену загрузки.
    /// Возвращает false, если право уже получил другой экземпляр сервиса.
    /// </summary>
    Task<Result<bool, Error>> TryBeginAbortAsync(
        Guid mediaAssetId,
        string uploadId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Пытается получить право на очистку истёкшей сессии.
    /// Также позволяет продолжить зависшую операцию ABORTING после таймаута.
    /// </summary>
    Task<Result<bool, Error>> TryBeginExpirationAsync(
        Guid mediaAssetId,
        string uploadId,
        DateTime now,
        CancellationToken cancellationToken = default);
}
