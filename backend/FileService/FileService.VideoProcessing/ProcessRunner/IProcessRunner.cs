using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace FileService.VideoProcessing.ProcessRunner;

public interface IProcessRunner
{
    Task<Result<ProcessResult, Error>> RunAsync(
        ProcessCommand processCommand,
        Action<string?> onOutput = null!,
        CancellationToken cancellationToken = default);
}