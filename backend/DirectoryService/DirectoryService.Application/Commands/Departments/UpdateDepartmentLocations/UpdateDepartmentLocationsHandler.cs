using CSharpFunctionalExtensions;
using DirectoryService.Application.Database;
using DirectoryService.Contracts.ValueObjects.Ids;
using DirectoryService.Domain.Entities;
using FluentValidation;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using SharedService.Core.Abstractions;
using SharedService.Core.Validation;
using SharedService.SharedKernel;

namespace DirectoryService.Application.Commands.Departments.UpdateDepartmentLocations;

public class UpdateDepartmentLocationsHandler : ICommandHandler<Guid, UpdateDepartmentLocationsCommand>
{
    private readonly ITransactionManager _transactionManager;
    private readonly ILocationRepository _locationRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IValidator<UpdateDepartmentLocationsCommand> _validator;
    private readonly HybridCache _cache;
    private readonly ILogger<UpdateDepartmentLocationsHandler> _logger;

    public UpdateDepartmentLocationsHandler(

        ITransactionManager transactionManager,
        IDepartmentRepository departmentRepository,
        IValidator<UpdateDepartmentLocationsCommand> validator,
        HybridCache cache,
        ILocationRepository locationRepository,
        ILogger<UpdateDepartmentLocationsHandler> logger)
    {
        _transactionManager = transactionManager;
        _locationRepository = locationRepository;
        _departmentRepository = departmentRepository;
        _validator = validator;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Result<Guid, Failure>> Handle(UpdateDepartmentLocationsCommand command,
        CancellationToken cancellationToken)
    {
        if (command == null)
            return Errors.General.ValueIsInvalid("command").ToFailure();

        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            _logger.LogWarning("Command is invalid!");

            return validationResult.ToErrors();
        }

        var transactionScopeResult = await _transactionManager.BeginTransactionAsync(cancellationToken);
        if (transactionScopeResult.IsFailure)
            return transactionScopeResult.Error.ToFailure();

        using var transactionScope = transactionScopeResult.Value;

        var departmentResult = await _departmentRepository.GetById(command.DepartmentId, cancellationToken);
        if (departmentResult.IsFailure)
        {
            transactionScope.Rollback();
            return Errors.General.NotFoundEntity("departmentId").ToFailure();
        }

        var department = departmentResult.Value;

        var locationIds = command.Request.LocationIds.Select(id => LocationId.Create(id));
        var distinctLocationIds = locationIds.Distinct().ToList();

        var locationsCheck = await CheckLocationsExist(distinctLocationIds, cancellationToken);
        if (locationsCheck.IsFailure)
        {
            transactionScope.Rollback();
            return locationsCheck.Error.ToFailure();
        }

        List<DepartmentLocation> newDepartmentLocations = [];
        foreach (var locationId in distinctLocationIds)
        {
            var depLocation = DepartmentLocation.Create(locationId, department.Id);
            if(depLocation.IsFailure)
            {
                transactionScope.Rollback();
                return depLocation.Error.ToFailure();
            }

            newDepartmentLocations.Add(depLocation.Value);
        }

        await _departmentRepository.DeleteDepartmentLocationsByDepartmentId(department.Id, cancellationToken);

        department.AddDepartmentLocation(newDepartmentLocations);

        await _transactionManager.SaveChangeAsync(cancellationToken);

        var commitedResult = transactionScope.Commit();
        if (commitedResult.IsFailure)
        {
            return commitedResult.Error.ToFailure();
        }

        await _cache.RemoveByTagAsync("departments", cancellationToken);
        _logger.LogInformation("Cache with tag: 'departments' was cleaned");

        return department.Id.Value;
    }

    private async Task<UnitResult<Error>> CheckLocationsExist(
        IEnumerable<LocationId> locationIds,
        CancellationToken cancellationToken)
    {
        var idList = locationIds.ToList();

        return await _locationRepository.AllLocationsExistAsync(
            idList,
            cancellationToken);
    }
}
