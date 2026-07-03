using System.Linq.Expressions;
using CSharpFunctionalExtensions;
using FileService.Domain.MediaProcessing;
using SharedService.SharedKernel;

namespace FileService.Core.Abstractions;

public interface IVideoProcessesRepository
{
    Task<UnitResult<Error>> DeleteVideoProcessesById(
        Guid videoProcessId,
        CancellationToken cancellationToken = default);

    Result<Guid, Error> Add(VideoProcess videoProcess);

    Task<Result<VideoProcess, Error>> GetBy(Expression<Func<VideoProcess, bool>> predicate,
        CancellationToken cancellationToken = default);

    Task<Result<VideoProcess, Error>> GetById(Guid videoProcessId, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<RecoverableVideoProcess>, Error>> GetRecoverableVideoProcessesAsync(
        CancellationToken cancellationToken = default);
}

public sealed record RecoverableVideoProcess(
    Guid VideoAssetId,
    string CorrelationId,
    DateTime? NextRetryAt);
