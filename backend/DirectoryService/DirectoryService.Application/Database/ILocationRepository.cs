using CSharpFunctionalExtensions;
using DirectoryService.Domain.Entities;
using DirectoryService.SharedKernel;
using DirectoryService.SharedKernel.ValueObjects.Ids;

namespace DirectoryService.Application.Database;

public interface ILocationRepository
{
    Task<Result<Guid, Error>> Add(Location location, CancellationToken cancellationToken);
    Task<UnitResult<Error>> AllLocationsExistAsync(IEnumerable<LocationId> locationIds,
        CancellationToken cancellationToken);

    Task<Result<List<Location>, Error>> GetUniqDepRelatedLocations(Guid departmentId,
        CancellationToken cancellationToken = default);

    Task<UnitResult<Error>> SoftDeleteUniqDepRelatedLocations(Guid departmentId,
        CancellationToken cancellationToken = default);
}
