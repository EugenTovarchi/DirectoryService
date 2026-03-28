using System.Linq.Expressions;
using CSharpFunctionalExtensions;
using FileService.Core.Abstractions;
using FileService.Domain.MediaProcessing;
using FileService.VideoProcessing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using SharedService.SharedKernel;

namespace FileService.Infrastructure.Postgres.Repositories;

public class VideoProcessesRepository(
    FileServiceDbContext dbContext,
    ILogger<VideoProcessesRepository> logger)
    : IVideoProcessesRepository
{
    public async Task<UnitResult<Error>> DeleteVideoProcessesById(
        Guid videoProcessId,
        CancellationToken cancellationToken = default)
    {
        await dbContext.VideoProcesses
            .Where(m => m.Id == videoProcessId)
            .ExecuteDeleteAsync(cancellationToken);

        logger.LogInformation("Video process with id: {id} was deleted", videoProcessId);

        return UnitResult.Success<Error>();
    }

    public Result<Guid, Error> Add(VideoProcess videoProcess)
    {
        try
        {
            dbContext.VideoProcesses.Add(videoProcess);

            return videoProcess.Id;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx)
        {
            return HandlePostgresException(pgEx, videoProcess.Id);
        }
        catch (OperationCanceledException ex)
        {
            logger.LogError(ex, "Operation was cancelled while creating video process with id:{Id}",
                videoProcess.Id);
            return Errors.General.DatabaseError("creating_video_process_error");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while creating video process with id {id}", videoProcess.Id);
            return Errors.General.DatabaseError("creating_video_process_error");
        }
    }

    public async Task<Result<VideoProcess, Error>> GetBy(Expression<Func<VideoProcess, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        VideoProcess? videoProcess =
            await dbContext.VideoProcesses
                .Include(v => v.Steps)
                .FirstOrDefaultAsync(predicate, cancellationToken);

        if (videoProcess is null)
            return Errors.General.NotFoundEntity("videoProcess");

        return videoProcess;
    }

    public async Task<Result<VideoProcess, Error>> GetById(Guid videoProcessId, CancellationToken cancellationToken)
    {
        var videoProcess = await dbContext.VideoProcesses
            .FirstOrDefaultAsync(v => v.Id == videoProcessId, cancellationToken);

        if (videoProcess is null)
            return Errors.General.NotFoundEntity("videoProcess");

        return videoProcess;
    }

    private Result<Guid, Error> HandlePostgresException(PostgresException pgEx, Guid videoProcessId)
    {
        if (pgEx.SqlState != PostgresErrorCodes.UniqueViolation || pgEx.File == null)
        {
            logger.LogError("Database error while creating video process {id}: {Message}", videoProcessId,
                pgEx.MessageText);
            return Errors.General.DatabaseError("creating_media_asset_error");
        }

        logger.LogError("Unknown unique constraint violation for video process {id}: {File}", videoProcessId,
            pgEx.File);
        return Errors.General.Duplicate("record");
    }
}