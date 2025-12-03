using CSharpFunctionalExtensions;
using DirectoryService.Domain.Entities;
using DirectoryService.SharedKernel;

namespace DirectoryService.Application.Database;

public interface IDepartmentRepository
{
    Task<Result<Department, Error>> GetById(Guid departmentId, CancellationToken cancellationToken);
    Task<Result<Guid, Error>> AddAsync(Department department, CancellationToken cancellationToken = default);
    Task<Result<bool, Error>> IsDepartmentExistAsync(Guid departmentId, CancellationToken cancellationToken = default);
    Task<Result<bool, Error>> AllDepartmentsExistAsync(IEnumerable<Guid> departmentsIds, CancellationToken cancellationToken);
}
