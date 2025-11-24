using DirectoryService.Domain.Entities;

namespace DirectoryService.Application;

public interface ILocationRepository
{
    Task<Guid> Add(Location location, CancellationToken cancellationToken);
}
