using CSharpFunctionalExtensions;
using Dapper;
using DirectoryService.Application.Database;
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
    private readonly INpgsqlConnectionFactory _connectionFactory;
    private readonly ILogger<PositionRepository> _logger;

    public PositionRepository(DirectoryServiceDbContext dbContext,
        INpgsqlConnectionFactory connectionFactory,
        ILogger<PositionRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
        _connectionFactory = connectionFactory;
    }

    public async Task<Result<Position, Error>> GetById(Guid positionId, CancellationToken cancellationToken)
    {
        var position = await _dbContext.Positions
            .FirstOrDefaultAsync(v => v.Id == positionId, cancellationToken);

        if (position is null)
            return Errors.General.ValueIsInvalid("position");

        return position;
    }

    public async Task<Result<List<Position>, Error>> GetUniqDepRelatedPositions(Guid departmentId,
        CancellationToken cancellationToken = default)
    {
        var uniqPositionIds = await _dbContext.DepartmentPositions
            .Where(dp => dp.DepartmentId == departmentId)
            .Where(dp => !_dbContext.DepartmentPositions
                .Any(dp2 => dp2.PositionId == dp.PositionId &&
                            dp2.DepartmentId != departmentId))
            .Select(dp => dp.PositionId)
            .ToListAsync(cancellationToken);

        if (uniqPositionIds.Count == 0)
            return new List<Position>();

        var positions = await _dbContext.Positions
            .Where(p => uniqPositionIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

        return positions;
    }

    public async Task<UnitResult<Error>> SoftDeleteUniqDepRelatedPositions(Guid departmentId,
        CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters();

        parameters.Add("@department_id", departmentId);
        parameters.Add("@deleted_at", DateTime.UtcNow);
        parameters.Add("@updated_at", DateTime.UtcNow);

        try
        {
             const string sql =
                """
                     WITH unique_positions AS (
                     SELECT dp1.position_id
                     FROM department_positions dp1
                     WHERE dp1.department_id = @department_id
                       AND NOT EXISTS (
                         SELECT 1
                         FROM department_positions dp2
                         WHERE dp2.position_id = dp1.position_id
                           AND dp2.department_id != @department_id
                     )
                 )
                 UPDATE positions p
                 SET is_deleted = true,
                     deleted_at = @deleted_at,
                     updated_at = @updated_at
                 FROM unique_positions 
                 WHERE p.id = unique_positions.position_id AND p.is_deleted = false;
                 """;
            
            var connection = _dbContext.Database.GetDbConnection();
            var updatedPositions = await connection.ExecuteAsync(sql, parameters);
            
            _logger.LogInformation("Count of updated positions: {updatedPostions}", updatedPositions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update error for positions of department{departmentId}", departmentId);
            return Errors.General.DatabaseError("update.positions");
        }

        return UnitResult.Success<Error>();
    }

    public async Task<Result<bool, Error>> IsPositionExistAsync(Guid positionId,
        CancellationToken cancellationToken = default)
    {
        var isPositionExist = await _dbContext.Departments
            .FirstOrDefaultAsync(p => p.Id == positionId, cancellationToken);

        if (isPositionExist is null)
            return Errors.General.NotFoundEntity("position");

        return true;
    }


    public async Task<Result<Guid, Error>> AddAsync(Position position, CancellationToken cancellationToken = default)
    {
        var existingPosition = await _dbContext.Positions
            .FirstOrDefaultAsync(p => p.Name.Value == position.Name.Value, cancellationToken);

        if (existingPosition != null)
        {
            _logger.LogWarning("Duplicate position name: {name}", position.Name.Value);
            return Errors.General.Duplicate("position_name");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await _dbContext.Positions.AddAsync(position, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return position.Id.Value;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx)
        {
            await transaction.RollbackAsync(cancellationToken);
            return HandlePostgresException(pgEx, position.Name.Value);
        }
        catch (OperationCanceledException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Operation was cancelled while creating position with name {name}",
                position.Name.Value);
            return Errors.General.DatabaseError("creating_position_error");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Unexpected error while creating position with name {name}", position.Name.Value);
            return Errors.General.DatabaseError("creating_position_error");
        }
    }

    private Result<Guid, Error> HandlePostgresException(PostgresException pgEx, string positionName)
    {
        if (pgEx.SqlState != PostgresErrorCodes.UniqueViolation || pgEx.ConstraintName == null)
        {
            _logger.LogError("Database error while creating position {name}: {Message}", positionName,
                pgEx.MessageText);
            return Errors.General.DatabaseError("creating_position_error");
        }

        var constraintName = pgEx.ConstraintName.ToLower();

        if (constraintName == "ix_position_name")
        {
            _logger.LogWarning("Duplicate position name: {name}", positionName);
            return Errors.General.Duplicate("position_name");
        }

        if (constraintName.Contains("name"))
        {
            _logger.LogWarning("Duplicate name constraint violation for position: {name}", positionName);
            return Errors.General.Duplicate("name");
        }


        _logger.LogError("Unknown unique constraint violation for position {name}: {Constraint}", positionName,
            pgEx.ConstraintName);
        return Errors.General.Duplicate("record");
    }
}