using CSharpFunctionalExtensions;
using DirectoryService.Application.Database;
using DirectoryService.Contracts.ValueObjects;
using DirectoryService.Contracts.ValueObjects.Ids;
using DirectoryService.Domain.Entities;
using FluentValidation;
using Microsoft.Extensions.Logging;
using SharedService.Core.Abstractions;
using SharedService.Core.Validation;
using SharedService.SharedKernel;

namespace DirectoryService.Application.Commands.Departments.Create;

public class CreateDepartmentHandler : ICommandHandler<Guid, CreateDepartmentCommand>
{
    private readonly ILocationRepository _locationRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IValidator<CreateDepartmentCommand> _validator;
    private readonly ILogger<CreateDepartmentHandler> _logger;
    public CreateDepartmentHandler(
        ILocationRepository locationRepository,
        IValidator<CreateDepartmentCommand> validator,
        ILogger<CreateDepartmentHandler> logger,
        IDepartmentRepository departmentRepository)
    {
        _locationRepository = locationRepository;
        _departmentRepository = departmentRepository;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<Guid, Failure>> Handle(CreateDepartmentCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command == null)
            return Errors.General.ValueIsInvalid("command").ToFailure();

        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            _logger.LogWarning("Department: {command} is invalid!", command.Request.DepartmentName);

            return validationResult.ToErrors();
        }

        var departmentResult = await CreateDepartment(command, cancellationToken);
        if (departmentResult.IsFailure)
            return departmentResult.Error.ToFailure();

        var department = departmentResult.Value;

        if (command.Request.LocationIds.Any())
        {
            var locationIds = command.Request.LocationIds.Select(id => LocationId.Create(id));

            var locationsCheck = await CheckLocationsExist(locationIds, cancellationToken);
            if (locationsCheck.IsFailure)
                return locationsCheck.Error.ToFailure();

            var addLocationsResult = AddLocations(locationIds, department);
            if (addLocationsResult.IsFailure)
                return addLocationsResult.Error.ToFailure();
        }

        var saveResult = await _departmentRepository.AddAsync(departmentResult.Value, cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error.ToFailure();

        _logger.LogInformation("Department {DepartmentId} created successfully", departmentResult.Value.Id.Value);
        return department.Id.Value;
    }

    private async Task<Result<Department, Error>> CreateDepartment(
        CreateDepartmentCommand command,
        CancellationToken cancellationToken)
    {
        var departmentName = Name.Create(command.Request.DepartmentName);
        if (departmentName.IsFailure)
            return departmentName.Error;

        var identifier = Identifier.Create(command.Request.Identifier);
        if (identifier.IsFailure)
            return identifier.Error;

        if (command.Request.ParentId is null)
        {
            return Department.CreateRoot(departmentName.Value, identifier.Value);
        }
        else
        {
            var parent = await _departmentRepository.GetById(
                DepartmentId.Create(command.Request.ParentId.Value),
                cancellationToken);

            if (parent.IsFailure)
                return Errors.General.NotFoundEntity("parent");

            return Department.CreateChild(
                departmentName.Value,
                identifier.Value,
                parent.Value);
        }
    }

    private async Task<UnitResult<Error>> CheckLocationsExist(
        IEnumerable<LocationId> locationIds,
        CancellationToken cancellationToken)
    {
        var distinctIds = locationIds.Distinct().ToList();

        var checkResult = await _locationRepository.AllLocationsExistAsync(
            distinctIds,
            cancellationToken);
        if (checkResult.IsFailure)
            return checkResult.Error;

        return checkResult;
    }

    private UnitResult<Error> AddLocations(
        IEnumerable<LocationId> locationIds,
        Department department)
    {
        foreach (var locationId in locationIds)
        {
            var addLocationResult = department.AddLocation(locationId);
            if (addLocationResult.IsFailure)
                return addLocationResult.Error;
        }

        return Result.Success<Error>();
    }
}
