using System.Linq.Expressions;
using CSharpFunctionalExtensions;
using DirectoryService.Contracts.ValueObjects.Ids;
using DirectoryService.Domain.Entities;
using SharedService.SharedKernel;

namespace DirectoryService.Application.Database;

public interface IDepartmentRepository
{
    Task<Result<Department, Error>> GetById(Guid departmentId, CancellationToken cancellationToken = default);
    Task<Result<Guid, Error>> AddAsync(Department department, CancellationToken cancellationToken = default);
    Task<Result<bool, Error>> IsDepartmentExistAsync(Guid departmentId, CancellationToken cancellationToken = default);
    Task<Result<bool, Error>> AllDepartmentsExistAsync(List<Guid> departmentsIds,
        CancellationToken cancellationToken = default);
    Task<UnitResult<Error>> DeleteDepartmentLocationsByDepartmentId(DepartmentId departmentId,
        CancellationToken cancellationToken = default);

    Task<UnitResult<Error>> MarkDepartmentAsDeleted(
        string prefix,
        DepartmentId deletedDepartmentId,
        CancellationToken cancellationToken);

    Task<Result<Department, Error>> GetBy(Expression<Func<Department, bool>> predicate,
        CancellationToken cancellationToken = default);

    Task<Result<Department, Error>> GetByIdWithLock(Guid departmentId, CancellationToken cancellationToken);
    Task<UnitResult<Error>> LockDescendantsByPath(string oldPath, CancellationToken cancellationToken);
    Task<UnitResult<Error>> UpdateAllDescendantsPath(
     string oldPath,
     string newPath,
     DepartmentId movedDepartmentId,
     CancellationToken cancellationToken);
}
