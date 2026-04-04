using System.Linq.Expressions;
using CSharpFunctionalExtensions;
using FileService.Core;
using FileService.Core.Abstractions;
using FileService.Domain.Assets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using SharedService.SharedKernel;

namespace FileService.Infrastructure.Postgres.Repositories;

public class MediaAssetsRepository(
    FileServiceDbContext dbContext,
    ILogger<MediaAssetsRepository> logger)
    : IMediaAssetsRepository
{
    public async Task<UnitResult<Error>> DeleteMediaAssetById(
        Guid mediaAssetId,
        CancellationToken cancellationToken = default)
    {
        await dbContext.MediaAssets
            .Where(m => m.Id == mediaAssetId)
            .ExecuteDeleteAsync(cancellationToken);

        logger.LogInformation("Media asset with id: {id} was deleted", mediaAssetId);

        return UnitResult.Success<Error>();
    }

    public Result<Guid, Error> Add(MediaAsset mediaAsset)
    {
        try
        {
            dbContext.MediaAssets.Add(mediaAsset);

            return mediaAsset.Id;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx)
        {
            return HandlePostgresException(pgEx, mediaAsset.Id);
        }
        catch (OperationCanceledException ex)
        {
            logger.LogError(ex, "Operation was cancelled while creating media asset with id:{Id}",
                mediaAsset.Id);
            return Errors.General.DatabaseError("creating_media_asset_error");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while creating media asset with id {id}", mediaAsset.Id);
            return Errors.General.DatabaseError("creating_media_asset_error");
        }
    }

    public async Task<Result<MediaAsset, Error>> GetBy(Expression<Func<MediaAsset, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        MediaAsset? mediaAsset = await dbContext.MediaAssets.AsTracking().FirstOrDefaultAsync(predicate, cancellationToken);
        if (mediaAsset is null)
            return Errors.General.NotFoundEntity("mediaAsset");

        return mediaAsset;
    }

    public async Task<Result<VideoAsset, Error>> GetVideoBy(Expression<Func<VideoAsset, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        VideoAsset? videoAsset = await dbContext.MediaAssets
            .OfType<VideoAsset>()
            .FirstOrDefaultAsync(predicate, cancellationToken);
        if (videoAsset is null)
            return Errors.General.NotFoundEntity("videoAsset");

        return videoAsset;
    }

    public async Task<Result<MediaAsset, Error>> GetById(Guid mediaAssetId, CancellationToken cancellationToken)
    {
        var mediaAsset = await dbContext.MediaAssets
            .FirstOrDefaultAsync(v => v.Id == mediaAssetId, cancellationToken);

        if (mediaAsset is null)
            return Errors.General.NotFoundEntity("mediaAsset");

        return mediaAsset;
    }

    private Result<Guid, Error> HandlePostgresException(PostgresException pgEx, Guid mediaAssetId)
    {
        if (pgEx.SqlState != PostgresErrorCodes.UniqueViolation || pgEx.File == null)
        {
            logger.LogError("Database error while creating media asset {id}: {Message}", mediaAssetId,
                pgEx.MessageText);
            return Errors.General.DatabaseError("creating_media_asset_error");
        }

        logger.LogError("Unknown unique constraint violation for media asset {id}: {File}", mediaAssetId,
            pgEx.File);
        return Errors.General.Duplicate("record");
    }
}