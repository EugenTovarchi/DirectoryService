using CSharpFunctionalExtensions;
using DirectoryService.Contracts.ValueObjects.Ids;
using DirectoryService.Domain.Entities;
using SharedService.SharedKernel;

namespace DirectoryService.Application.Database;

public interface ILocationRepository
{
    Task<Result<Guid, Error>> Add(Location location, CancellationToken cancellationToken);

    Task<UnitResult<Error>> AllLocationsExistAsync(IEnumerable<LocationId> locationIds, CancellationToken cancellationToken);

    Task<UnitResult<Error>> SoftDeleteUniqDepRelatedLocations(Guid departmentId, CancellationToken cancellationToken = default);
}
