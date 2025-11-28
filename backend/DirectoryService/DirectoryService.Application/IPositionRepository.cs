using CSharpFunctionalExtensions;
using DirectoryService.Domain.Entities;
using DirectoryService.SharedKernel;

namespace DirectoryService.Application;

public interface IPositionRepository
{
    Task<Result<Guid, Error>> AddAsync(Position position, CancellationToken cancellationToken = default);
}
