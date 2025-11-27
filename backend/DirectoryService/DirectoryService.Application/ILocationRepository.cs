using CSharpFunctionalExtensions;
using DirectoryService.Domain.Entities;
using DirectoryService.SharedKernel;

namespace DirectoryService.Application;

public interface ILocationRepository
{
    Task<Result<Guid, Error>> Add(Location location, CancellationToken cancellationToken);
}
