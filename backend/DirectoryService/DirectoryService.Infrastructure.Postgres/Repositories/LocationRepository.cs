using CSharpFunctionalExtensions;
using Dapper;
using DirectoryService.Application.Database;
using DirectoryService.Domain.Entities;
using DirectoryService.Infrastructure.Postgres.DbContexts;
using DirectoryService.SharedKernel;
using DirectoryService.SharedKernel.ValueObjects.Ids;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace DirectoryService.Infrastructure.Postgres.Repositories;

public class LocationRepository(DirectoryServiceDbContext dbContext, ILogger<LocationRepository> logger)
    : ILocationRepository
{
    public async Task<Result<bool, Error>> IsLocationExistAsync(
        Guid locationId,
        CancellationToken cancellationToken = default)
    {
        var isLocationExist = await dbContext.Locations
            .FirstOrDefaultAsync(l => l.Id == locationId, cancellationToken);

        if (isLocationExist is null)
            return Errors.General.NotFoundEntity("location");

        return true;
    }

    public async Task<UnitResult<Error>> SoftDeleteUniqDepRelatedLocations(Guid departmentId,
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
                    WITH unique_locations AS (
                    SELECT dl1.location_id
                    FROM department_locations dl1
                    WHERE dl1.department_id = @department_id
                      AND NOT EXISTS (
                        SELECT 1
                        FROM department_locations dl2
                        WHERE dl2.location_id = dl1.location_id
                          AND dl2.department_id != @department_id
                    )
                )
                UPDATE locations l
                SET is_deleted = true,
                    deleted_at = @deleted_at,
                    updated_at = @updated_at
                FROM unique_locations 
                WHERE l.id = unique_locations.location_id AND l.is_deleted = false;
                """;

            var connection = dbContext.Database.GetDbConnection();
            var updatedLocations = await connection.ExecuteAsync(sql, parameters);

            logger.LogInformation("Count of updated locations: {updatedLocations}", updatedLocations);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Update error for locations of department{departmentId}", departmentId);
            return Errors.General.DatabaseError("update.locations");
        }

        return UnitResult.Success<Error>();
    }

    public async Task<UnitResult<Error>> AllLocationsExistAsync(
        IEnumerable<LocationId> locationIds,
        CancellationToken cancellationToken)
    {
        var idList = locationIds.ToList();

        if (idList.Count == 0)
            return Errors.General.ValueIsEmpty("locationIds");

        int count = await dbContext.Locations
            .CountAsync(l => idList.Contains(l.Id) && !l.IsDeleted, cancellationToken);

        return count == idList.Count
            ? Result.Success<Error>()
            : Errors.General.NotFoundEntity("location");
    }

    public async Task<Result<Guid, Error>> Add(Location location, CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.Locations.AddAsync(location, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return location.Id.Value;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx)
        {
            return HandlePostgresException(pgEx, location.Name.Value);
        }
        catch (OperationCanceledException ex)
        {
            logger.LogError(ex, "Operation was cancelled while creating location with name {name}",
                location.Name.Value);
            return Errors.General.DatabaseError("creating_location_error");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while creating location with name {name}", location.Name.Value);
            return Errors.General.DatabaseError("creating_location_error");
        }
    }

    private Result<Guid, Error> HandlePostgresException(PostgresException pgEx, string locationName)
    {
        if (pgEx.SqlState != PostgresErrorCodes.UniqueViolation || pgEx.ConstraintName == null)
        {
            logger.LogError("Database error while creating location {name}: {Message}", locationName,
                pgEx.MessageText);
            return Errors.General.DatabaseError("creating_location_error");
        }

        string constraintName = pgEx.ConstraintName.ToLower();

        switch (constraintName)
        {
            case "ix_location_name":
                logger.LogWarning("Duplicate location name: {name}", locationName);
                return Errors.General.Duplicate("location_name");
            case "ix_location_address_unique":
                logger.LogWarning("Duplicate location address for location: {name}", locationName);
                return Errors.General.Duplicate("address");
        }

        if (constraintName.Contains("name"))
        {
            logger.LogWarning("Duplicate name constraint violation for location: {name}", locationName);
            return Errors.General.Duplicate("name");
        }

        if (constraintName.Contains("address"))
        {
            logger.LogWarning("Duplicate address constraint violation for location: {name}", locationName);
            return Errors.General.Duplicate("address");
        }

        logger.LogError("Unknown unique constraint violation for location {name}: {Constraint}", locationName,
            pgEx.ConstraintName);
        return Errors.General.Duplicate("record");
    }
}