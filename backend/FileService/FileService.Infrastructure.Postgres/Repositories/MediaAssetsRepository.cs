using System.Linq.Expressions;
using CSharpFunctionalExtensions;
using FileService.Core;
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
    public async Task<Result<Guid, Error>> AddAsync(MediaAsset mediaAsset,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.MediaAssets.AddAsync(mediaAsset, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

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
        MediaAsset? mediaAsset = await dbContext.MediaAssets.FirstOrDefaultAsync(predicate, cancellationToken);
        if (mediaAsset is null)
            return Errors.General.NotFoundEntity("mediaAsset");

        return mediaAsset;
    }

    public async Task<UnitResult<Error>> SaveChangeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return UnitResult.Success<Error>();
        }
        catch(Exception ex)
        {
            logger.LogError(ex, "Failed to save changes");
            return Error.Failure("database", "Failed to save changes");
        }
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