using CSharpFunctionalExtensions;
using DirectoryService.Core.Abstractions;
using DirectoryService.Core.Validation;
using DirectoryService.Domain.Entities;
using DirectoryService.SharedKernel;
using DirectoryService.SharedKernel.ValueObjects;
using DirectoryService.SharedKernel.ValueObjects.Ids;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Path = DirectoryService.SharedKernel.ValueObjects.Path;

namespace DirectoryService.Application.Commands.Departments;

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

        var saveResult = await _departmentRepository.AddAsync(departmentResult.Value, cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error.ToFailure();

        _logger.LogInformation("Department {DepartmentId} created successfully", departmentResult.Value.Id.Value);
        return departmentResult.Value.Id.Value;
    }

    private async Task<Result<Department, Error>> CreateDepartment(
        CreateDepartmentCommand command,
        CancellationToken cancellationToken)
    {
        var locationsCheck = await CheckLocations(command.Request.LocationIds, cancellationToken);
        if (locationsCheck.IsFailure)
            return locationsCheck.Error;

        var departmentName = Name.Create(command.Request.DepartmentName);
        if (departmentName.IsFailure)
            return departmentName.Error;

        var identifier = Identifier.Create(command.Request.Identifier);
        if (identifier.IsFailure)
            return identifier.Error;

        var pathInfoResult = await CalculatePathAndDepth(command, cancellationToken);
        if (pathInfoResult.IsFailure)
            return pathInfoResult.Error;

        var pathInfo = pathInfoResult.Value;

        var department = Department.Create(
            departmentName.Value,
            identifier.Value,
            pathInfo.Path,
            pathInfo.Depth,
            pathInfo.ParentId);


        foreach (var locationId in command.Request.LocationIds)
        {
            var addLocationResult = department.Value.AddLocation(LocationId.Create(locationId));
            if (addLocationResult.IsFailure)
                return addLocationResult.Error;
        }

        return department;
    }

    private async Task<Result<DepartmentPathInfo, Error>> CalculatePathAndDepth(
        CreateDepartmentCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Request.ParentId == null)
        {
            var path = Path.Create(command.Request.Identifier);
            if (path.IsFailure)
                return path.Error;

            return new DepartmentPathInfo(path.Value, 0, null);
        }
        else
        {
            var parentResult = await _departmentRepository.GetById(
                DepartmentId.Create(command.Request.ParentId.Value),
                cancellationToken);

            if (parentResult.IsFailure)
                return Errors.General.NotFoundEntity("parent_department");

            var parent = parentResult.Value;

            var fullPath = $"{parent.Path.Value}.{command.Request.Identifier}";
            var path = Path.Create(fullPath);
            if (path.IsFailure)
                return path.Error;

            short depth = (short)(parent.Depth + 1);

            return new DepartmentPathInfo(path.Value, depth, command.Request.ParentId);
        }
    }

    private async Task<Result<bool, Error>> CheckLocations(IEnumerable<Guid> locationIds, CancellationToken ct = default)
    {
        foreach (var locationId in locationIds)
        {
            var locationExists = await _locationRepository.IsLocationExistAsync(locationId, ct);
            if (locationExists.IsFailure)
                return locationExists.Error;
        }

        return true;
    }
}
