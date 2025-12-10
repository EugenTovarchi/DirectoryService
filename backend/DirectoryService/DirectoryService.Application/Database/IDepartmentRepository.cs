using CSharpFunctionalExtensions;
using DirectoryService.Domain.Entities;
using DirectoryService.SharedKernel;
using DirectoryService.SharedKernel.ValueObjects.Ids;

namespace DirectoryService.Application.Database;

public interface IDepartmentRepository
{
    Task<Result<Department, Error>> GetById(Guid departmentId, CancellationToken cancellationToken = default);
    Task<Result<Guid, Error>> AddAsync(Department department, CancellationToken cancellationToken = default);
    Task<Result<bool, Error>> IsDepartmentExistAsync(Guid departmentId, CancellationToken cancellationToken = default);
    Task<Result<bool, Error>> AllDepartmentsExistAsync(IEnumerable<Guid> departmentsIds, CancellationToken cancellationToken = default);
    Task<UnitResult<Error>> DeleteDepartmentLocationsByDepartmentId(DepartmentId departmentId,
        CancellationToken cancellationToken = default);

    Task<Result<Department, Error>> GetByIdWithLock(Guid departmentId, CancellationToken cancellationToken);
    Task<UnitResult<Error>> LockDescendantsByPath(string oldPath, CancellationToken cancellationToken);
}
