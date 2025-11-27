using CSharpFunctionalExtensions;
using DirectoryService.Core.Abstractions;
using DirectoryService.Core.Validation;
using DirectoryService.Domain.Entities;
using DirectoryService.SharedKernel;
using DirectoryService.SharedKernel.ValueObjects;
using FluentValidation;
using Microsoft.Extensions.Logging;
using TimeZone = DirectoryService.SharedKernel.ValueObjects.TimeZone;

namespace DirectoryService.Application.Commands.Locations.CreateLocation;

public class CreateLocationHandler : ICommandHandler<Guid, CreateLocationCommand>
{
    private readonly ILocationRepository _locationRepository;
    private readonly IValidator<CreateLocationCommand> _validator;
    private readonly ILogger<CreateLocationCommand> _logger;

    public CreateLocationHandler(
        ILocationRepository locationRepository,
        IValidator<CreateLocationCommand> validator,
        ILogger<CreateLocationCommand> logger)
    {
        _locationRepository = locationRepository;
        _validator = validator;
        _logger = logger;
    }
    public async Task<Result<Guid, Failure>> Handle(CreateLocationCommand command, CancellationToken ct = default)
    {
        if (command == null)
            return Errors.General.ValueIsInvalid("command").ToFailure();

        var validationResult = await _validator.ValidateAsync(command, ct);
        if (!validationResult.IsValid)
        {
            _logger.LogWarning("Вид с {command} не валиден!", command.Request.LocationName);

            return validationResult.ToErrors();
        }

        var locationName = Name.Create(command.Request.LocationName).Value;

        var locationTimeZone = TimeZone.Create(command.Request.TimeZone).Value;


        var locationAddress = command.Request.LocationAddress.Flat is null
        ? Address.Create(
        command.Request.LocationAddress.Country,
        command.Request.LocationAddress.City,
        command.Request.LocationAddress.Street,
        command.Request.LocationAddress.House).Value
        : Address.CreateWithFlat(
        command.Request.LocationAddress.Country,
        command.Request.LocationAddress.City,
        command.Request.LocationAddress.Street,
        command.Request.LocationAddress.House,
        command.Request.LocationAddress.Flat.Value).Value;

        var locationResult = Location.Create(locationName, locationTimeZone, locationAddress);
        if (locationResult.IsFailure)
            return locationResult.Error.ToFailure();

        var result = await _locationRepository.Add(locationResult.Value, ct);
        if(result.IsFailure)
            return result.Error.ToFailure();

        _logger.LogInformation("Location: {location} was created", locationResult.Value.Id.Value);

        return locationResult.Value.Id.Value;
    }
}
