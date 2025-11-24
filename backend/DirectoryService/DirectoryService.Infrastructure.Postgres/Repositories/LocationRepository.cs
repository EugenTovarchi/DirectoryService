using DirectoryService.Application;
using DirectoryService.Domain.Entities;
using DirectoryService.Infrastructure.Postgres.DbContexts;
using Microsoft.Extensions.Logging;


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

    public async Task<Guid> Add(Location location, CancellationToken cancellationToken)
    {
        await _dbContext.Locations.AddAsync(location, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return location.Id;
    }
}
