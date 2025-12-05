using CSharpFunctionalExtensions;
using DirectoryService.Application.Database;
using DirectoryService.Core.Abstractions;
using DirectoryService.Core.Validation;
using DirectoryService.Domain.Entities;
using DirectoryService.SharedKernel;
using DirectoryService.SharedKernel.ValueObjects.Ids;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace DirectoryService.Application.Commands.Departments.UpdateDepartmentLocations;

public class UpdateDepartmentLocationsHandler : ICommandHandler<Guid, UpdateDepartmentLocationsCommand>
{
    private readonly ITrasactionManager _trasactionManager;
    private readonly ILocationRepository _locationRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IValidator<UpdateDepartmentLocationsCommand> _validator;
    private readonly ILogger<UpdateDepartmentLocationsHandler> _logger;

    public UpdateDepartmentLocationsHandler(
        ITrasactionManager trasactionManager,
        ILocationRepository locationRepository,
        IDepartmentRepository departmentRepository,
        IValidator<UpdateDepartmentLocationsCommand> validator,
        ILogger<UpdateDepartmentLocationsHandler> logger)
    {
        _trasactionManager = trasactionManager;
        _locationRepository = locationRepository;
        _departmentRepository = departmentRepository;
        _validator = validator;
        _logger = logger;
    }
    public async Task<Result<Guid, Failure>> Handle(UpdateDepartmentLocationsCommand command, CancellationToken cancellationToken)
    {
        if (command == null)
            return Errors.General.ValueIsInvalid("command").ToFailure();

        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            _logger.LogWarning("Command is invalid!");

            return validationResult.ToErrors();
        }

        var transactionScopeResult = await _trasactionManager.BeginTransactionAsync(cancellationToken);
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

        var locationsCheck = await CheckLocationsExist(locationIds, cancellationToken);
        if (locationsCheck.IsFailure)
        {
            transactionScope.Rollback();
            return locationsCheck.Error.ToFailure();
        }

        List<DepartmentLocation> newDepartmentLocations = [];
        foreach (var locationId in locationIds)
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

        await _trasactionManager.SaveChangeAsync(cancellationToken);

        var commitedResult = transactionScope.Commit();
        if (commitedResult.IsFailure)
        {
            return commitedResult.Error.ToFailure();
        }

        return department.Id.Value;
    }

    private async Task<UnitResult<Error>> CheckLocationsExist(
        IEnumerable<LocationId> locationIds,
        CancellationToken cancellationToken)
    {
        var distinctIds = locationIds.Distinct().ToList();

        return await _locationRepository.AllLocationsExistAsync(
            distinctIds,
            cancellationToken);
    }
}
