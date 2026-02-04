using CSharpFunctionalExtensions;
using DirectoryService.Application.Database;
using DirectoryService.Contracts.ValueObjects;
using DirectoryService.Contracts.ValueObjects.Ids;
using DirectoryService.Core.Abstractions;
using DirectoryService.Core.Validation;
using DirectoryService.Domain.Entities;
using DirectoryService.SharedKernel;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace DirectoryService.Application.Commands.Positions.Create;

public class CreatePositionHandler : ICommandHandler<Guid, CreatePositionCommand>
{
    private readonly IPositionRepository _positionRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IValidator<CreatePositionCommand> _validator;
    private readonly ILogger<CreatePositionHandler> _logger;
    public CreatePositionHandler(
        IPositionRepository positionRepository,
        IDepartmentRepository departmentRepository,
        IValidator<CreatePositionCommand> validator,
        ILogger<CreatePositionHandler> logger)
    {
        _positionRepository = positionRepository;
        _departmentRepository = departmentRepository;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<Guid, Failure>> Handle(CreatePositionCommand command, CancellationToken cancellationToken)
    {
        if (command == null)
            return Errors.General.ValueIsInvalid("command").ToFailure();

        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            _logger.LogWarning("Position: {command} is invalid!", command.Request.PositionName);

            return validationResult.ToErrors();
        }

        var positionName = Name.Create(command.Request.PositionName);
        if (positionName.IsFailure)
            return positionName.Error.ToFailure();

        var positionResult = command.Request.Description is not null
            ? Position.CreateWithDescription(positionName.Value, Description.Create(command.Request.Description).Value)
            : Position.CreateWithoutDescription(positionName.Value);
        if (positionResult.IsFailure)
            return positionResult.Error.ToFailure();

        var position = positionResult.Value;

        if (command.Request.DepartmentIds.Any())
        {
            var departmentsCheck = await CheckDepartments(command.Request.DepartmentIds, cancellationToken);
            if (departmentsCheck.IsFailure)
                return departmentsCheck.Error.ToFailure();

            var addDepartmentPositionResult = AddDepartmentPosition(command.Request.DepartmentIds, position);
            if (addDepartmentPositionResult.IsFailure)
                return addDepartmentPositionResult.Error.ToFailure();
        }

        var saveResult = await _positionRepository.AddAsync(position, cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error.ToFailure();

        _logger.LogInformation("Position {PositionId} created successfully", positionResult.Value.Id.Value);
        return positionResult.Value.Id.Value;
    }

    private async Task<UnitResult<Error>> CheckDepartments(
    IEnumerable<Guid> departmentIds,
    CancellationToken cancellationToken)
    {
        var distinctIds = departmentIds.Distinct().ToList();

        var allExistResult = await _departmentRepository.AllDepartmentsExistAsync(
            distinctIds,
            cancellationToken);

        if (allExistResult.IsFailure)
            return allExistResult.Error;

        if(!allExistResult.Value)
            return Errors.General.NotFoundEntity("departments");

        return Result.Success<Error>();
    }

    private static UnitResult<Error> AddDepartmentPosition(
        IEnumerable<Guid> departmentIds,
        Position position)
    {
        foreach (var departmentId in departmentIds)
        {
            var addLocationResult = position.AddDepartmentPosition(DepartmentId.Create(departmentId));
            if (addLocationResult.IsFailure)
                return addLocationResult.Error;
        }

        return Result.Success<Error>();
    }
}
