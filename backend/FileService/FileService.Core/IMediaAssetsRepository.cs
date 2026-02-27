using System.Linq.Expressions;
using CSharpFunctionalExtensions;
using FileService.Domain.Assets;
using SharedService.SharedKernel;

namespace FileService.Core;

public interface IMediaAssetsRepository
{
    Task<Result<Guid, Error>> AddAsync(MediaAsset mediaAsset, CancellationToken cancellationToken = default);

    Task<Result<MediaAsset, Error>> GetBy(Expression<Func<MediaAsset, bool>> predicate,
        CancellationToken cancellationToken = default);

    Task<UnitResult<Error>> SaveChangeAsync(CancellationToken cancellationToken);
}