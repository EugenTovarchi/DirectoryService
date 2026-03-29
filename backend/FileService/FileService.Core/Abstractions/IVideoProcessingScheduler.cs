using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace FileService.Core.Abstractions;

public interface IVideoProcessingScheduler
{
    Task<UnitResult<Error>> ScheduleProcessingAsync(
        Guid videoAssetId,
        CancellationToken cancellationToken = default);
}