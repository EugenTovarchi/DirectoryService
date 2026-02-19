using CSharpFunctionalExtensions;
using FileService.Domain.Assets;
using SharedService.SharedKernel;

namespace FileService.Core;

public interface IMediaAssetsRepository
{
    Task<Result<Guid, Error>> AddAsync(MediaAsset mediaAsset, CancellationToken cancellationToken = default);
}