using CSharpFunctionalExtensions;
using FileService.Domain.MediaProcessing.VO;
using SharedService.SharedKernel;

namespace FileService.VideoProcessing.FfmpegProcess;

public interface IFfmpegProcessRunner
{
    Task<UnitResult<Error>> GenerateHlsAsync(
        string inputFileUrl,
        string outputDirectory,
        CancellationToken cancellationToken = default);

    Task<Result<VideoMetadata, Error>> ExtractMetadataAsync(string inputFileUrl,
        CancellationToken cancellationToken = default);
}