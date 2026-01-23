using CSharpFunctionalExtensions;
using DirectoryService.Domain.Entities;
using DirectoryService.SharedKernel;

namespace DirectoryService.Application.Database;

public interface IPositionRepository
{
    Task<Result<Guid, Error>> AddAsync(Position position, CancellationToken cancellationToken = default);

    Task<Result<List<Position>, Error>> GetUniqDepRelatedPositions(Guid departmentId,
        CancellationToken cancellationToken = default);

    Task<UnitResult<Error>> SoftDeleteUniqDepRelatedPositions(Guid departmentId,
        CancellationToken cancellationToken = default);
}
