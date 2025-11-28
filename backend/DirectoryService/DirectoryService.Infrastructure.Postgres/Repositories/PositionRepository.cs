using CSharpFunctionalExtensions;
using DirectoryService.Application;
using DirectoryService.Domain.Entities;
using DirectoryService.Infrastructure.Postgres.DbContexts;
using DirectoryService.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace DirectoryService.Infrastructure.Postgres.Repositories;

public class PositionRepository : IPositionRepository
{
    private readonly DirectoryServiceDbContext _dbContext;
    private readonly ILogger<PositionRepository> _logger;

    public PositionRepository(DirectoryServiceDbContext dbContext, ILogger<PositionRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }
    public async Task<Result<Position, Error>> GetById(Guid positionId, CancellationToken cancellationToken)
    {
        var position = await _dbContext.Positions
            .FirstOrDefaultAsync(v => v.Id == positionId, cancellationToken);

        if (position is null)
            return Errors.General.ValueIsInvalid("position");

        return position;
    }

    public async Task<Result<bool, Error>> IsPositionExistAsync(Guid positionId, CancellationToken cancellationToken = default)
    {
        var isPositionExist = await _dbContext.Departments
            .FirstOrDefaultAsync(p => p.Id == positionId, cancellationToken);

        if (isPositionExist is null)
            return Errors.General.NotFoundEntity("position");

        return true;
    }

    public async Task<Result<Guid, Error>> AddAsync(Position position, CancellationToken cancellationToken = default)
    {
        await _dbContext.Positions.AddAsync(position, cancellationToken);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return position.Id.Value;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx)
        {
            return HandlePostgresException(pgEx, position.Name.Value);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogError(ex, "Operation was cancelled while creating position with name {name}", position.Name.Value);
            return Errors.General.DatabaseError("creating_department_error");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while creating position with name {name}", position.Name.Value);
            return Errors.General.DatabaseError("creating_position_error");
        }
    }

    private Result<Guid, Error> HandlePostgresException(PostgresException pgEx, string positionName)
    {
        if (pgEx.SqlState != PostgresErrorCodes.UniqueViolation || pgEx.ConstraintName == null)
        {
            _logger.LogError("Database error while creating position {name}: {Message}", positionName, pgEx.MessageText);
            return Errors.General.DatabaseError("creating_department_error");
        }

        var constraintName = pgEx.ConstraintName.ToLower();

        if (constraintName == "ix_position_name")
        {
            _logger.LogWarning("Duplicate position name: {name}", positionName);
            return Errors.General.Duplicate("department_name");
        }
        
        if (constraintName.Contains("name"))
        {
            _logger.LogWarning("Duplicate name constraint violation for position: {name}", positionName);
            return Errors.General.Duplicate("name");
        }


        _logger.LogError("Unknown unique constraint violation for position {name}: {Constraint}", positionName, pgEx.ConstraintName);
        return Errors.General.Duplicate("record");
    }
}
