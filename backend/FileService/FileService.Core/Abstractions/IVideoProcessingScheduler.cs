using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace FileService.Core.Abstractions;

public interface IVideoProcessingScheduler
{
    Task<UnitResult<Error>> ScheduleProcessingAsync(
        Guid videoAssetId,
        string correlationId,
        DateTimeOffset? startAt = null,
        CancellationToken cancellationToken = default);
}
