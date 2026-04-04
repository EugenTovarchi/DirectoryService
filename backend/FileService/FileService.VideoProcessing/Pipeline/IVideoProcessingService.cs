using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace FileService.VideoProcessing.Pipeline;

public interface IVideoProcessingService
{
    Task<UnitResult<Error>> ProcessVideoAsync(
        Guid videoAssetId,
        CancellationToken cancellationToken = default);
}