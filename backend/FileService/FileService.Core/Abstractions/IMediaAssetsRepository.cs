using System.Linq.Expressions;
using CSharpFunctionalExtensions;
using FileService.Domain.Assets;
using SharedService.SharedKernel;

namespace FileService.Core;

public interface IMediaAssetsRepository
{
    Result<Guid, Error> Add(MediaAsset mediaAsset);

    Task<Result<MediaAsset, Error>> GetBy(Expression<Func<MediaAsset, bool>> predicate,
        CancellationToken cancellationToken = default);

    Task<UnitResult<Error>> DeleteMediaAssetById(Guid mediaAssetId, CancellationToken cancellationToken = default);

    Task<Result<MediaAsset, Error>> GetById(Guid mediaAssetId, CancellationToken cancellationToken);

    Task<Result<VideoAsset, Error>> GetVideoBy(Expression<Func<VideoAsset, bool>> predicate,
        CancellationToken cancellationToken = default);
}