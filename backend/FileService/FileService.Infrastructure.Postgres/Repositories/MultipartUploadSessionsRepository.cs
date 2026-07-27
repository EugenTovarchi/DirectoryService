using CSharpFunctionalExtensions;
using FileService.Core.Abstractions;
using FileService.Core.FilesStorage;
using FileService.Domain.Uploads;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedService.SharedKernel;

namespace FileService.Infrastructure.Postgres.Repositories;

/// <summary>
/// Координирует экземпляры сервиса через условные UPDATE в PostgreSQL.
/// Распределённая блокировка не требуется: право на операцию получает экземпляр,
/// чей запрос изменил одну строку.
/// </summary>
public sealed class MultipartUploadSessionsRepository(
    FileServiceDbContext dbContext,
    IOptions<MultipartUploadOptions> options,
    ILogger<MultipartUploadSessionsRepository> logger)
    : IMultipartUploadSessionsRepository
{
    public Result<Guid, Error> Add(MultipartUploadSession session)
    {
        dbContext.MultipartUploadSessions.Add(session);
        return session.Id;
    }

    public async Task<Result<MultipartUploadSession, Error>> GetByMediaAssetIdAsync(
        Guid mediaAssetId,
        CancellationToken cancellationToken = default)
    {
        MultipartUploadSession? session = await dbContext.MultipartUploadSessions
            .FirstOrDefaultAsync(
                uploadSession => uploadSession.MediaAssetId == mediaAssetId,
                cancellationToken);

        return session is null
            ? Errors.General.NotFoundEntity("multipartUploadSession")
            : session;
    }

    public Task<MultipartUploadSession?> FindByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return dbContext.MultipartUploadSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                session => session.IdempotencyKey == idempotencyKey,
                cancellationToken);
    }

    public Task<Result<bool, Error>> TryBeginCompletionAsync(
        Guid mediaAssetId,
        string uploadId,
        CancellationToken cancellationToken = default)
    {
        return TryClaimAsync(
            mediaAssetId,
            uploadId,
            MultipartUploadStatus.ACTIVE,
            MultipartUploadStatus.COMPLETING,
            cancellationToken);
    }

    public Task<Result<bool, Error>> TryBeginAbortAsync(
        Guid mediaAssetId,
        string uploadId,
        CancellationToken cancellationToken = default)
    {
        return TryClaimAsync(
            mediaAssetId,
            uploadId,
            MultipartUploadStatus.ACTIVE,
            MultipartUploadStatus.ABORTING,
            cancellationToken);
    }

    public async Task<Result<bool, Error>> TryBeginExpirationAsync(
        Guid mediaAssetId,
        string uploadId,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        try
        {
            DateTime retryAllowedBefore = now.AddMinutes(-options.Value.OperationTimeoutMinutes);

            // Помимо ACTIVE разрешаем продолжить зависшую ABORTING: это восстанавливает очистку,
            // если предыдущий экземпляр упал после изменения статуса, но до сохранения результата.
            int affectedRows = await dbContext.MultipartUploadSessions
                .Where(session =>
                    session.MediaAssetId == mediaAssetId
                    && session.UploadId == uploadId
                    && (session.Status == MultipartUploadStatus.ACTIVE
                        || (session.Status == MultipartUploadStatus.ABORTING
                            && session.UpdatedAt < retryAllowedBefore))
                    && session.ExpiresAt <= now)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(session => session.Status, MultipartUploadStatus.ABORTING)
                        .SetProperty(session => session.UpdatedAt, now)
                        .SetProperty(session => session.Version, Guid.NewGuid()),
                    cancellationToken);

            return affectedRows == 1;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to begin expiration for media asset {MediaAssetId}",
                mediaAssetId);
            return Errors.General.DatabaseError("expiring_multipart_upload_error");
        }
    }

    private async Task<Result<bool, Error>> TryClaimAsync(
        Guid mediaAssetId,
        string uploadId,
        MultipartUploadStatus expectedStatus,
        MultipartUploadStatus claimedStatus,
        CancellationToken cancellationToken)
    {
        try
        {
            DateTime now = DateTime.UtcNow;
            DateTime retryAllowedBefore = now.AddMinutes(-options.Value.OperationTimeoutMinutes);
            Guid nextVersion = Guid.NewGuid();

            // Условный UPDATE является точкой координации всех экземпляров сервиса.
            // Только один экземпляр изменит строку и получит право выполнить операцию в S3.
            int affectedRows = await dbContext.MultipartUploadSessions
                .Where(session =>
                    session.MediaAssetId == mediaAssetId
                    && session.UploadId == uploadId
                    && (session.Status == expectedStatus
                        || (session.Status == claimedStatus
                            && session.UpdatedAt < retryAllowedBefore))
                    && session.ExpiresAt > now)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(session => session.Status, claimedStatus)
                        .SetProperty(session => session.UpdatedAt, now)
                        .SetProperty(session => session.Version, nextVersion),
                    cancellationToken);

            return affectedRows == 1;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to claim multipart upload operation for media asset {MediaAssetId}",
                mediaAssetId);
            return Errors.General.DatabaseError("claiming_multipart_upload_error");
        }
    }
}
