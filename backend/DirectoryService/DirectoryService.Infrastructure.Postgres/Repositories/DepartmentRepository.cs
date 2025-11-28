using CSharpFunctionalExtensions;
using IDepartmentRepository = DirectoryService.Application.IDepartmentRepository;
using DirectoryService.Domain.Entities;
using DirectoryService.Infrastructure.Postgres.DbContexts;
using DirectoryService.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

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
    public async Task<Result<Department, Error>> GetById(Guid departmentId, CancellationToken cancellationToken)
    {
        var department = await _dbContext.Departments
            .FirstOrDefaultAsync(v => v.Id == departmentId, cancellationToken);

        if (department is null)
            return Errors.General.ValueIsInvalid("department");

        return department;
    }

    public async Task<Result<bool, Error>> IsDepartmentExistAsync(Guid departmentId, CancellationToken cancellationToken = default)
    {
        var isDepartmentExist = await _dbContext.Departments
            .FirstOrDefaultAsync(l => l.Id == departmentId, cancellationToken);

        if (isDepartmentExist is null)
            return Errors.General.NotFoundEntity("department");

        return true;
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
