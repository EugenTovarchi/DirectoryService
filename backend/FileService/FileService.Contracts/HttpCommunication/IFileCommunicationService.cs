using CSharpFunctionalExtensions;
using FileService.Contracts.Requests;
using FileService.Contracts.Responses;
using SharedService.SharedKernel;

namespace FileService.Contracts.HttpCommunication;

public interface IFileCommunicationService
{
    Task<Result<GetMediaAssetResponse, Failure>> GetMediaAssetInfo(Guid mediaAssetId,
        CancellationToken cancellationToken);

    Task<Result<GetMediaAssetsResponse, Failure>> GetMediaAssetsInfo(GetMediaAssetsRequest request,
        CancellationToken cancellationToken);

    Task<Result<CheckMediaAssetExistResponse, Failure>> CheckMediaAssetExists(Guid mediaAssetId,
        CancellationToken cancellationToken);
}