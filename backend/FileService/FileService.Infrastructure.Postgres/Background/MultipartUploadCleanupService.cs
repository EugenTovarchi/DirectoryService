using CSharpFunctionalExtensions;
using FileService.Core.Abstractions;
using FileService.Core.FilesStorage;
using FileService.Domain;
using FileService.Domain.Assets;
using FileService.Domain.Uploads;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedService.SharedKernel;

namespace FileService.Infrastructure.Postgres.Background;

/// <summary>
/// Периодически завершает жизненный цикл просроченных multipart-сессий:
/// получает право на обработку в PostgreSQL, отменяет загрузку в S3
/// и сохраняет состояние для последующего анализа.
/// </summary>
public sealed class MultipartUploadCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<MultipartUploadOptions> options,
    ILogger<MultipartUploadCleanupService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredUploadsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error while cleaning up expired multipart uploads");
            }

            await Task.Delay(
                TimeSpan.FromMinutes(options.Value.CleanupIntervalMinutes),
                stoppingToken);
        }
    }

    /// <summary>
    /// Выполняет один ограниченный набор операций очистки. Метод открыт для
    /// детерминированного запуска в интеграционных тестах без ожидания таймера.
    /// </summary>
    public async Task CleanupExpiredUploadsAsync(CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;
        IReadOnlyList<ExpiredUpload> expiredUploads = await FindExpiredUploadsAsync(now, cancellationToken);

        foreach (ExpiredUpload upload in expiredUploads)
        {
            await CleanupUploadAsync(upload, now, cancellationToken);
        }
    }

    private async Task<IReadOnlyList<ExpiredUpload>> FindExpiredUploadsAsync(
        DateTime now,
        CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FileServiceDbContext>();
        DateTime retryAllowedBefore = now.AddMinutes(-options.Value.OperationTimeoutMinutes);

        var rows = await dbContext.MultipartUploadSessions
            .AsNoTracking()
            .Where(session =>
                (session.Status == MultipartUploadStatus.ACTIVE
                    || (session.Status == MultipartUploadStatus.ABORTING
                        && session.UpdatedAt < retryAllowedBefore))
                && session.ExpiresAt <= now
                && session.UploadId != null)
            .Join(
                dbContext.MediaAssets.AsNoTracking(),
                session => session.MediaAssetId,
                mediaAsset => mediaAsset.Id,
                (session, mediaAsset) => new { Session = session, MediaAsset = mediaAsset })
            .OrderBy(row => row.Session.ExpiresAt)
            .Take(options.Value.CleanupBatchSize)
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new ExpiredUpload(
                row.Session.MediaAssetId,
                row.Session.UploadId!,
                row.MediaAsset.UploadKey))
            .ToList();
    }

    private async Task CleanupUploadAsync(
        ExpiredUpload upload,
        DateTime now,
        CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        var sessionsRepository =
            scope.ServiceProvider.GetRequiredService<IMultipartUploadSessionsRepository>();

        Result<bool, Error> claimResult = await sessionsRepository.TryBeginExpirationAsync(
            upload.MediaAssetId,
            upload.UploadId,
            now,
            cancellationToken);
        if (claimResult.IsFailure || !claimResult.Value)
            return;

        // Операцию в S3 выполняет только экземпляр, который получил право на обработку.
        var storageProvider = scope.ServiceProvider.GetRequiredService<IFileStorageProvider>();
        UnitResult<Error> abortResult = await storageProvider.AbortMultipartUploadAsync(
            upload.StorageKey,
            upload.UploadId,
            cancellationToken);

        var mediaAssetsRepository = scope.ServiceProvider.GetRequiredService<IMediaAssetsRepository>();
        Result<MediaAsset, Error> mediaAssetResult = await mediaAssetsRepository.GetById(
            upload.MediaAssetId,
            cancellationToken);
        Result<MultipartUploadSession, Error> sessionResult =
            await sessionsRepository.GetByMediaAssetIdAsync(upload.MediaAssetId, cancellationToken);
        if (mediaAssetResult.IsFailure || sessionResult.IsFailure)
            return;

        MediaAsset mediaAsset = mediaAssetResult.Value;
        MultipartUploadSession session = sessionResult.Value;

        // NoSuchUpload эквивалентен успешной очистке: внешнего ресурса уже нет.
        if (abortResult.IsSuccess || abortResult.Error.Code == "upload.id")
        {
            UnitResult<Error> expirationResult = session.Expire(now);
            UnitResult<Error> mediaFailureResult = mediaAsset.MarkFailed();
            if (expirationResult.IsFailure || mediaFailureResult.IsFailure)
            {
                logger.LogError(
                    "Failed to apply successful cleanup state for media asset {MediaAssetId}",
                    upload.MediaAssetId);
                return;
            }
        }
        else if (StorageErrorClassifier.IsRetryable(abortResult.Error))
        {
            UnitResult<Error> releaseResult =
                session.ReleaseAbort($"Retryable S3 cleanup error with code {abortResult.Error.Code}");
            if (releaseResult.IsFailure)
            {
                logger.LogError(
                    "Failed to release cleanup claim for media asset {MediaAssetId}",
                    upload.MediaAssetId);
                return;
            }
        }
        else
        {
            UnitResult<Error> sessionFailureResult =
                session.Fail($"S3 cleanup failed with code {abortResult.Error.Code}");
            UnitResult<Error> mediaFailureResult = mediaAsset.MarkFailed();
            if (sessionFailureResult.IsFailure || mediaFailureResult.IsFailure)
            {
                logger.LogError(
                    "Failed to apply permanent cleanup failure for media asset {MediaAssetId}",
                    upload.MediaAssetId);
                return;
            }
        }

        var transactionManager = scope.ServiceProvider.GetRequiredService<ITransactionManager>();
        UnitResult<Error> saveResult = await transactionManager.SaveChangeAsync(cancellationToken);
        if (saveResult.IsFailure)
        {
            logger.LogError(
                "Failed to persist cleanup result for media asset {MediaAssetId}. Error code: {ErrorCode}",
                upload.MediaAssetId,
                saveResult.Error.Code);
        }
    }

    private sealed record ExpiredUpload(
        Guid MediaAssetId,
        string UploadId,
        StorageKey StorageKey);
}
