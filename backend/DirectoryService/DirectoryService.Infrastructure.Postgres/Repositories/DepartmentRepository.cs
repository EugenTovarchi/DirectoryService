using CSharpFunctionalExtensions;
using DirectoryService.Domain.Entities;
using DirectoryService.Infrastructure.Postgres.DbContexts;
using DirectoryService.SharedKernel;
using DirectoryService.SharedKernel.ValueObjects.Ids;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using IDepartmentRepository = DirectoryService.Application.Database.IDepartmentRepository;

namespace DirectoryService.Infrastructure.Postgres.Repositories;

public  class DepartmentRepository: IDepartmentRepository
{
    private readonly DirectoryServiceDbContext _dbContext;
    private readonly ILogger<DepartmentRepository> _logger;

    public DepartmentRepository(DirectoryServiceDbContext dbContext, ILogger<DepartmentRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<UnitResult<Error>> UpdateAsRoot(Department department)
    {
        var pathResult = SharedKernel.ValueObjects.Path.Create(department.Identifier.Value);
        if (pathResult.IsFailure)
            return pathResult.Error;

        await _dbContext.Departments
            .Where(d => d.Id == department.Id)
            .ExecuteUpdateAsync(setter => setter
            .SetProperty(d => d.Path, pathResult.Value)
            .SetProperty(d => d.Depth, 0)
            .SetProperty(d => d.ParentId, null as DepartmentId)
            .SetProperty(d => d.UpdatedAt, DateTime.UtcNow));

        return Result.Success<Error>();
    }

    public async Task<UnitResult<Error>> UpdateChildrenPathAndDepth(
    Department department,
    Department parent)
    {
        var pathResult = SharedKernel.ValueObjects.Path.CreateForChild(
            parent.Path,
            department.Identifier);

        if (pathResult.IsFailure)
            return pathResult.Error;

        var newPath = pathResult.Value;
        var newDepth = (short)(parent.Depth + 1);   
        var newParentId = parent.Id;
        var updatedAt = DateTime.UtcNow;

        await _dbContext.Departments
            .Where(d => d.Id == department.Id)
            .ExecuteUpdateAsync(setter => setter
                .SetProperty(d => d.Path, newPath)
                .SetProperty(d => d.Depth, newDepth)
                .SetProperty(d => d.ParentId, newParentId)
                .SetProperty(d => d.UpdatedAt, updatedAt));

        return Result.Success<Error>();
    }

    public async Task<Result<Department, Error>> GetById(Guid departmentId, CancellationToken cancellationToken)
    {
        var department = await _dbContext.Departments
           .FirstOrDefaultAsync(v => v.Id == departmentId, cancellationToken);

        if (department is null)
            return Errors.General.NotFoundEntity("department");

        return department;
    }

    public async Task<Result<Department, Error>> GetByIdWithLock(Guid departmentId, CancellationToken cancellationToken)
    {
        var department = await _dbContext.Departments
        .FromSql($"SELECT * FROM departments WHERE id = {departmentId} AND is_deleted = false FOR UPDATE")
        .FirstOrDefaultAsync(cancellationToken);

        if (department is null)
            return Errors.General.ValueIsInvalid("department");

        return department;
    }

    public async Task<UnitResult<Error>> LockDescendantsByPath(string oldPath, CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.Database.ExecuteSqlRawAsync(
            "SELECT id FROM departments WHERE path <@ @oldPath::ltree AND is_deleted = false FOR UPDATE",
            new NpgsqlParameter("oldPath", oldPath));

            return UnitResult.Success<Error>();
        }
        catch (PostgresException pgEx) when (pgEx.SqlState == PostgresErrorCodes.LockNotAvailable)
        {
            _logger.LogWarning("Could not lock descendants of {Path} - already locked", oldPath);
            return Errors.General.ResourceLocked("department.descendants");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error locking descendants by path: {Path}", oldPath);
            return Errors.General.DatabaseError("lock.descendants");
        }
    }

    public async Task<UnitResult<Error>> UpdateAllDescendants(
        string oldPath,
        string newPath,
        DepartmentId movedDepartmentId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.Database.ExecuteSqlRawAsync(
            """
            UPDATE departments dept
            SET 
                path = @NewPath::ltree || subpath(dept.path, nlevel(@OldPath::ltree)),
                depth = nlevel(@NewPath::ltree) + (dept.depth - nlevel(@OldPath::ltree)),
                updated_at = @UpdatedAt
            WHERE dept.is_deleted = false
                    AND dept.path <@ @OldPath::ltree
                    AND dept.path != @OldPath::ltree
                    AND dept.id != @MovedDepartmentId
            """,
            new NpgsqlParameter("OldPath", oldPath),
            new NpgsqlParameter("NewPath", newPath),
            new NpgsqlParameter("MovedDepartmentId", movedDepartmentId.Value),
            new NpgsqlParameter("UpdatedAt", DateTime.UtcNow));

            return UnitResult.Success<Error>();
        }
        
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update error for descendats of department{movedDepartmentId}", movedDepartmentId);
            return Errors.General.DatabaseError("update.descendants");
        }
    }

    public async Task<Result<bool, Error>> IsDepartmentExistAsync(Guid departmentId, CancellationToken cancellationToken = default)
    {
        var isDepartmentExist = await _dbContext.Departments
            .FirstOrDefaultAsync(l => l.Id == departmentId, cancellationToken);

        if (isDepartmentExist is null)
            return Errors.General.NotFoundEntity("department");

        return true;
    }
    
    public async Task<UnitResult<Error>>DeleteDepartmentLocationsByDepartmentId(
        DepartmentId departmentId,
        CancellationToken cancellationToken= default)
    {
        await _dbContext.DepartmentLocations
           .Where(dl => dl.DepartmentId == departmentId)
           .ExecuteDeleteAsync(cancellationToken);

        return UnitResult.Success<Error>();
    }

    public async Task<Result<bool, Error>> AllDepartmentsExistAsync(
        IEnumerable<Guid> departmentsIds,
        CancellationToken cancellationToken)
    {
        try
        {
            var requestedCount = departmentsIds.ToList().Count;

            var existingCount = await _dbContext.Departments
            .Where(l => departmentsIds.Contains(l.Id) && !l.IsDeleted)
            .Select(l => l.Id)
            .Distinct()
            .CountAsync(cancellationToken);

            return requestedCount == existingCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking departments existence");
            return Errors.General.DatabaseError("check.departments");
        }
    }

    public async Task<Result<Guid, Error>> AddAsync(Department department, CancellationToken cancellationToken = default)
    {
        await _dbContext.Departments.AddAsync(department, cancellationToken);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return department.Id.Value;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx)
        {
            return HandlePostgresException(pgEx, department.Name.Value);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogError(ex, "Operation was cancelled while creating department with name {name}", department.Name.Value);
            return Errors.General.DatabaseError("creating_department_error");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while creating department with name {name}", department.Name.Value);
            return Errors.General.DatabaseError("creating_department_error");
        }
    }

    private Result<Guid, Error> HandlePostgresException(PostgresException pgEx, string departmentName)
    {
        if (pgEx.SqlState != PostgresErrorCodes.UniqueViolation || pgEx.ConstraintName == null)
        {
            _logger.LogError("Database error while creating department {name}: {Message}", departmentName, pgEx.MessageText);
            return Errors.General.DatabaseError("creating_department_error");
        }

        var constraintName = pgEx.ConstraintName.ToLower();

        if (constraintName == "ix_department_name")
        {
            _logger.LogWarning("Duplicate department name: {name}", departmentName);
            return Errors.General.Duplicate("department_name");
        }
        if (constraintName == "ix_department_idenfier")
        {
            _logger.LogWarning("Duplicate department identifier: {identifier}", departmentName);
            return Errors.General.Duplicate("department_name");
        }

        if (constraintName.Contains("name"))
        {
            _logger.LogWarning("Duplicate name constraint violation for department: {name}", departmentName);
            return Errors.General.Duplicate("name");
        }


        _logger.LogError("Unknown unique constraint violation for department {name}: {Constraint}", departmentName, pgEx.ConstraintName);
        return Errors.General.Duplicate("record");
    }
}
