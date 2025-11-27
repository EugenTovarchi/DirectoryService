using CSharpFunctionalExtensions;
using DirectoryService.Application;
using DirectoryService.Domain.Entities;
using DirectoryService.Infrastructure.Postgres.DbContexts;
using DirectoryService.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;


namespace DirectoryService.Infrastructure.Postgres.Repositories;

public class LocationRepository : ILocationRepository
{
    private readonly DirectoryServiceDbContext _dbContext;
    private readonly ILogger<LocationRepository> _logger;

    public LocationRepository(DirectoryServiceDbContext dbContext, ILogger<LocationRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Result<Guid, Error>> Add(Location location, CancellationToken cancellationToken = default)
    {
        await _dbContext.Locations.AddAsync(location, cancellationToken);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return location.Id.Value;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx)             
        {
            return HandlePostgresException(pgEx, location.Name.Value);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogError(ex, "Operation was cancelled while creating location with name {name}", location.Name.Value);
            return Errors.General.DatabaseError("creating_location_error");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while creating location with name {name}", location.Name.Value);
            return Errors.General.DatabaseError("creating_location_error");
        }
    }

    private Result<Guid, Error> HandlePostgresException(PostgresException pgEx, string locationName)
    {
        if (pgEx.SqlState != PostgresErrorCodes.UniqueViolation || pgEx.ConstraintName == null)
        {
            _logger.LogError("Database error while creating location {name}: {Message}", locationName, pgEx.MessageText);
            return Errors.General.DatabaseError("creating_location_error");
        }

        var constraintName = pgEx.ConstraintName.ToLower();

        if (constraintName == "ix_location_name")
        {
            _logger.LogWarning("Duplicate location name: {name}", locationName);
            return Errors.General.Duplicate("location_name");
        }

        if (constraintName == "ix_location_address_unique")
        {
            _logger.LogWarning("Duplicate location address for location: {name}", locationName);
            return Errors.General.Duplicate("address");
        }

        if (constraintName.Contains("name"))
        {
            _logger.LogWarning("Duplicate name constraint violation for location: {name}", locationName);
            return Errors.General.Duplicate("name");
        }

        if (constraintName.Contains("address"))
        {
            _logger.LogWarning("Duplicate address constraint violation for location: {name}", locationName);
            return Errors.General.Duplicate("address");
        }

        _logger.LogError("Unknown unique constraint violation for location {name}: {Constraint}", locationName, pgEx.ConstraintName);
        return Errors.General.Duplicate("record");
    }
}
