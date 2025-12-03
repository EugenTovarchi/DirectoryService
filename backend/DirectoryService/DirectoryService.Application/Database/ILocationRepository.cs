using CSharpFunctionalExtensions;
using DirectoryService.Domain.Entities;
using DirectoryService.SharedKernel;

namespace DirectoryService.Application.Database;

public interface ILocationRepository
{
    Task<Result<Guid, Error>> Add(Location location, CancellationToken cancellationToken);
    Task<Result<bool, Error>> AllLocationsExistAsync(IEnumerable<Guid> locationIds, CancellationToken cancellationToken);
}
