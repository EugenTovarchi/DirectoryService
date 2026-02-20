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
        var existingMediaAsset = await dbContext.MediaAssets
            .FirstOrDefaultAsync(m => m.Id == mediaAsset.Id, cancellationToken);

        if (existingMediaAsset != null)
        {
            logger.LogWarning("Duplicate mediaAsset id: {Id}", mediaAsset.Id);
            return Errors.General.Duplicate("mediaAsset_id");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await dbContext.MediaAssets.AddAsync(mediaAsset, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return mediaAsset.Id;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx)
        {
            await transaction.RollbackAsync(cancellationToken);
            return HandlePostgresException(pgEx, mediaAsset.Id);
        }
        catch (OperationCanceledException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogError(ex, "Operation was cancelled while creating media asset with id:{Id}",
                mediaAsset.Id);
            return Errors.General.DatabaseError("creating_media_asset_error");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogError(ex, "Unexpected error while creating media asset with id {id}", mediaAsset.Id);
            return Errors.General.DatabaseError("creating_media_asset_error");
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