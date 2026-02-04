using System.Linq.Expressions;
using CSharpFunctionalExtensions;
using Dapper;
using DirectoryService.Contracts.ValueObjects.Ids;
using DirectoryService.Domain.Entities;
using DirectoryService.Infrastructure.Postgres.DbContexts;
using DirectoryService.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Npgsql;
using IDepartmentRepository = DirectoryService.Application.Database.IDepartmentRepository;
using Path = DirectoryService.Contracts.ValueObjects.Path;

namespace DirectoryService.Infrastructure.Postgres.Repositories;

public class DepartmentRepository(
    DirectoryServiceDbContext dbContext,
    ILogger<DepartmentRepository> logger)
    : IDepartmentRepository
{
    public async Task<Result<Department, Error>> GetBy(Expression<Func<Department, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        var department = await dbContext.Departments.FirstOrDefaultAsync(predicate, cancellationToken);
        if (department is null)
            return Errors.General.NotFoundEntity("department");

        return department;
    }

    public async Task<List<Guid>> GetExpiredDepartmentsIds(int daysBeforeDeletion,
        CancellationToken cancellationToken = default)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-daysBeforeDeletion);

        var parameters = new DynamicParameters();
        parameters.Add("@cut_off_date", cutoffDate);

        const string sql = """
                               SELECT  d.id
                               FROM departments d
                               WHERE d.is_deleted = true AND  d.deleted_at <= @cut_off_date 
                           """;

        var connection = dbContext.Database.GetDbConnection();
        var expiredDepartments = await connection.QueryAsync<Guid>(sql, parameters);

        return expiredDepartments.ToList();
    }

    public async Task<List<Guid>> GetDescendantDepartmentIds(
        List<Guid> expiredDepartmentsIds,
        CancellationToken cancellationToken)
    {
        if (expiredDepartmentsIds.Count == 0)
            return [];

        var ids = expiredDepartmentsIds.ToArray();
        var parameters = new DynamicParameters();
        parameters.Add("@expiredDepartmentsIds", ids);

        const string sql = """
                               SELECT DISTINCT d.id
                               FROM departments d
                               INNER JOIN departments del ON d.path <@ del.path 
                                                          AND d.path != del.path
                                                          AND del.id = ANY(@expiredDepartmentsIds)
                               WHERE d.is_deleted = false
                           """;

        var connection = dbContext.Database.GetDbConnection();
        var descendants = await connection.QueryAsync<Guid>(sql, parameters);

        return descendants.ToList();
    }

    public async Task<int> CleanExpiredDepartmentsWithRelatesAsync(List<Guid> expiredDepartmentsIds,
        CancellationToken cancellationToken = default)
    {
        if (expiredDepartmentsIds.Count == 0)
            return 0;

        var ids = expiredDepartmentsIds.ToArray();
        var parameters = new DynamicParameters();
        parameters.Add("@ids", ids);

        const string sql = """

                           WITH deleted_dep_locations AS (
                               DELETE FROM department_locations 
                               WHERE department_id = ANY(@ids)
                           ),
                           deleted_dep_positions AS (
                               DELETE FROM department_positions 
                               WHERE department_id = ANY(@ids)
                           ),

                           deleted_departments AS (
                           DELETE FROM departments 
                           WHERE id = ANY(@ids)
                           RETURNING 1
                           )
                           SELECT COUNT(*) FROM deleted_departments;
                           """;

        var connection = dbContext.Database.GetDbConnection();

        var countCleanedDepartments = await connection.ExecuteScalarAsync<int>(
            sql,
            parameters,
            transaction: dbContext.Database.CurrentTransaction?.GetDbTransaction(),
            commandTimeout: 30);

        return countCleanedDepartments;
    }

    public async Task<UnitResult<Error>> UpdateAsRoot(Department department)
    {
        var pathResult = Path.Create(department.Identifier.Value);
        if (pathResult.IsFailure)
            return pathResult.Error;

        await dbContext.Departments
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
        var pathResult = Path.CreateForChild(
            parent.Path,
            department.Identifier);

        if (pathResult.IsFailure)
            return pathResult.Error;

        var newPath = pathResult.Value;
        var newDepth = (short)(parent.Depth + 1);
        var newParentId = parent.Id;
        var updatedAt = DateTime.UtcNow;

        await dbContext.Departments
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
        var department = await dbContext.Departments
            .FirstOrDefaultAsync(v => v.Id == departmentId, cancellationToken);

        if (department is null)
            return Errors.General.NotFoundEntity("department");

        return department;
    }

    public async Task<Result<Department, Error>> GetByIdWithLock(Guid departmentId, CancellationToken cancellationToken)
    {
        var department = await dbContext.Departments
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
            await dbContext.Database.ExecuteSqlRawAsync(
                "SELECT id FROM departments WHERE path <@ @oldPath::ltree AND is_deleted = false FOR UPDATE",
                new NpgsqlParameter("oldPath", oldPath));

            return UnitResult.Success<Error>();
        }
        catch (PostgresException pgEx) when (pgEx.SqlState == PostgresErrorCodes.LockNotAvailable)
        {
            logger.LogWarning("Could not lock descendants of {Path} - already locked", oldPath);
            return Errors.General.ResourceLocked("department.descendants");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error locking descendants by path: {Path}", oldPath);
            return Errors.General.DatabaseError("lock.descendants");
        }
    }

    public async Task<UnitResult<Error>> LockDescendantsByIds(
        List<Guid> departmentIdsToLock,
        CancellationToken cancellationToken = default)
    {
        if (departmentIdsToLock.Count == 0)
            return Errors.Validation.RecordIsInvalid("descendantIdsToLock");

        var ids = departmentIdsToLock.ToArray();

        var parameters = new DynamicParameters();
        parameters.Add("@ids", ids);

        try
        {
            var connection = dbContext.Database.GetDbConnection();

            const string sql = """  
                                       SELECT id FROM departments WHERE id = ANY(@ids)   
                                         AND is_deleted = false   
                                       FOR UPDATE  NOWAIT
                               """;

            var lockedDepIds = await connection.QueryAsync(sql, parameters);
            var lockedCount = lockedDepIds.Count();

            if (departmentIdsToLock.Count != lockedCount)
            {
                logger.LogWarning("Not all departments locked!");
                return Errors.Database.ResourceLocked("locked_dep_descendants");
            }

            logger.LogInformation("Successfully locked descendants: {lockedCount}", lockedCount);
            return UnitResult.Success<Error>();
        }
        catch (PostgresException pgEx) when (pgEx.SqlState == PostgresErrorCodes.LockNotAvailable)
        {
            logger.LogWarning("Descendants are already locked by other transactions");
            return Errors.Database.ResourceLocked("department_descendants");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error locking department_descendants");
            return Errors.General.DatabaseError("lock.department_descendants");
        }
    }

    public async Task<UnitResult<Error>> UpdateAllDescendantsPath(
        string oldPath,
        string newPath,
        DepartmentId parentDepartmentId,
        CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();

        parameters.Add("@oldPath", oldPath);
        parameters.Add("@newPath", newPath);
        parameters.Add("@parent_department_id", parentDepartmentId.Value);
        parameters.Add("@updated_at", DateTime.UtcNow);

        try
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                $"""
                 UPDATE departments dept
                 SET 
                     path = @newPath::ltree || subpath(dept.path, nlevel(@oldPath::ltree)),
                     depth = nlevel(@newPath::ltree) + (dept.depth - nlevel(@oldPath::ltree)),
                     updated_at = @updated_at
                 WHERE dept.is_deleted = false
                         AND dept.path <@ @oldPath::ltree
                         AND dept.id != @parent_department_id
                 """,
                parameters);

            return UnitResult.Success<Error>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Update error for descendants of department{parentDepartmentId}", parentDepartmentId);
            return Errors.General.DatabaseError("update.descendants");
        }
    }

    public async Task<UnitResult<Error>> MarkDepartmentAsDeleted(
        string prefix,
        DepartmentId deletedDepartmentId,
        CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();

        parameters.Add("@prefix", prefix);
        parameters.Add("@deleted_department_id", deletedDepartmentId.Value);

        try
        {
            const string sql =
                """
                UPDATE departments dept
                SET 
                    path =  subpath(dept.path, 0, -1) || (@prefix|| subpath(dept.path, -1)::text)::ltree
                    WHERE dept.is_deleted = true
                        AND dept.id = @deleted_department_id
                """;

            var connection = dbContext.Database.GetDbConnection();
            await connection.ExecuteAsync(sql, parameters);

            return UnitResult.Success<Error>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Path of department: {deletedDepartmentId} was updated",
                deletedDepartmentId.Value);
            return Errors.General.DatabaseError("update.descendant_path");
        }
    }

    /// <summary>
    /// Обновляет иерархическую информацию (path, parent_id, depth) для департаментов-потомков
    /// после удаления родительских департаментов. Удаляет из путей элементов с префиксом 'deleted_'
    /// и пересчитывает родительские связи.
    /// </summary>
    /// <param name="descendantIds">Список id департаментов-потомков для обновления</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Результат операции: успех или ошибка</returns>
    public async Task<UnitResult<Error>> UpdateDescendantsInfoAfterCleanUp(
        List<Guid> descendantIds,
        CancellationToken cancellationToken)
    {
        if (descendantIds.Count == 0)
            return Errors.Validation.RecordIsInvalid("descendantIds");

        var descendants = descendantIds.ToArray();

        var parameters = new DynamicParameters();
        parameters.Add("@descendant_ids", descendants);
        parameters.Add("@now", DateTime.UtcNow);

        try
        {
            const string sql =
                """
                WITH updated_paths AS (
                    SELECT 
                        d.id,
                        d.identifier,
                        d.path as old_path,
                        (
                            SELECT string_agg(elem, '.')::ltree
                            FROM (
                                SELECT unnest(string_to_array(d.path::text, '.')) as elem
                            ) as elements
                            WHERE NOT elem LIKE 'deleted_%'
                        ) as new_path
                    FROM departments d
                    WHERE d.id = ANY(@descendant_ids)
                      AND d.is_deleted = false
                      AND d.path::text LIKE '%deleted_%' 
                ),
                paths_with_parents AS (
                    SELECT 
                        up.id,
                        up.identifier,
                        up.old_path,
                        up.new_path,
                        CASE 
                            WHEN nlevel(up.new_path) = 1 THEN NULL
                            ELSE (
                                SELECT p.id
                                FROM departments p
                                WHERE p.path = subpath(up.new_path, 0, -1)
                                  AND p.is_deleted = false
                                LIMIT 1
                            )
                        END as parent_id
                    FROM updated_paths up
                    WHERE up.new_path IS NOT NULL
                      AND up.new_path != ''::ltree
                )
                UPDATE departments d
                SET 
                    path = pwp.new_path,
                    parent_id = pwp.parent_id,
                    depth = nlevel(pwp.new_path),
                    updated_at = @now
                FROM paths_with_parents pwp
                WHERE d.id = pwp.id;
                """;

            var connection = dbContext.Database.GetDbConnection();

            await connection.ExecuteAsync(
                sql,
                parameters,
                transaction: dbContext.Database.CurrentTransaction?.GetDbTransaction(),
                commandTimeout: 30);

            return UnitResult.Success<Error>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during updated department descendants info");
            return Errors.General.DatabaseError("update.descendant_info");
        }
    }

    public async Task<Result<bool, Error>> IsDepartmentExistAsync(
        Guid departmentId,
        CancellationToken cancellationToken = default)
    {
        var isDepartmentExist = await dbContext.Departments
            .FirstOrDefaultAsync(l => l.Id == departmentId, cancellationToken);

        if (isDepartmentExist is null)
            return Errors.General.NotFoundEntity("department");

        return true;
    }

    public async Task<UnitResult<Error>> DeleteDepartmentLocationsByDepartmentId(
        DepartmentId departmentId,
        CancellationToken cancellationToken = default)
    {
        await dbContext.DepartmentLocations
            .Where(dl => dl.DepartmentId == departmentId)
            .ExecuteDeleteAsync(cancellationToken);

        return UnitResult.Success<Error>();
    }

    public async Task<Result<bool, Error>> AllDepartmentsExistAsync(
        List<Guid> departmentsIds,
        CancellationToken cancellationToken)
    {
        try
        {
            var requestedCount = departmentsIds.ToList().Count;

            var existingCount = await dbContext.Departments
                .Where(l => departmentsIds.Contains(l.Id) && !l.IsDeleted)
                .Select(l => l.Id)
                .Distinct()
                .CountAsync(cancellationToken);

            return requestedCount == existingCount;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking departments existence");
            return Errors.General.DatabaseError("check.departments");
        }
    }

    public async Task<Result<Guid, Error>> AddAsync(Department department,
        CancellationToken cancellationToken = default)
    {
        var existingDepartment = await dbContext.Departments
            .FirstOrDefaultAsync(p => p.Name.Value == department.Name.Value, cancellationToken);

        if (existingDepartment != null)
        {
            logger.LogWarning("Duplicate department name: {name}", department.Name.Value);
            return Errors.General.Duplicate("department_name");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await dbContext.Departments.AddAsync(department, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return department.Id.Value;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx)
        {
            await transaction.RollbackAsync(cancellationToken);
            return HandlePostgresException(pgEx, department.Name.Value);
        }
        catch (OperationCanceledException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogError(ex, "Operation was cancelled while creating department with name {name}",
                department.Name.Value);
            return Errors.General.DatabaseError("creating_department_error");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogError(ex, "Unexpected error while creating department with name {name}", department.Name.Value);
            return Errors.General.DatabaseError("creating_department_error");
        }
    }

    private Result<Guid, Error> HandlePostgresException(PostgresException pgEx, string departmentName)
    {
        if (pgEx.SqlState != PostgresErrorCodes.UniqueViolation || pgEx.ConstraintName == null)
        {
            logger.LogError("Database error while creating department {name}: {Message}", departmentName,
                pgEx.MessageText);
            return Errors.General.DatabaseError("creating_department_error");
        }

        string constraintName = pgEx.ConstraintName.ToLower();

        switch (constraintName)
        {
            case "ix_department_name":
                logger.LogWarning("Duplicate department name: {name}", departmentName);
                return Errors.General.Duplicate("department_name");
            case "ix_department_identifier":
                logger.LogWarning("Duplicate department identifier: {identifier}", departmentName);
                return Errors.General.Duplicate("department_name");
        }

        if (constraintName.Contains("name"))
        {
            logger.LogWarning("Duplicate name constraint violation for department: {name}", departmentName);
            return Errors.General.Duplicate("name");
        }

        logger.LogError("Unknown unique constraint violation for department {name}: {Constraint}", departmentName,
            pgEx.ConstraintName);
        return Errors.General.Duplicate("record");
    }
}