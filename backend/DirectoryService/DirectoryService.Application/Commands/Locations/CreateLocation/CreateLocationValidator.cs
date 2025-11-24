using DirectoryService.Core.Validation;
using DirectoryService.SharedKernel;
using DirectoryService.SharedKernel.ValueObjects;
using FluentValidation;

namespace DirectoryService.Application.Commands.Locations.CreateLocation;

public class CreateLocationValidator : AbstractValidator<CreateLocationCommand>
{
    public CreateLocationValidator()
    {
        RuleFor(l => l.Request.LocationName)
         .NotEmpty().WithError(Errors.General.ValueIsEmptyOrWhiteSpace("LocationName"))
         .MaximumLength(120).WithError(Errors.Validation.RecordIsInvalid("LocationName"))
         .MinimumLength(3).WithError(Errors.Validation.RecordIsInvalid("LocationName"));

        RuleFor(l => l.Request.TimeZone)
            .NotEmpty().WithMessage("timeZone")
            .MaximumLength(50).WithMessage($"timeZone")
            .Must(BeValidTimeZoneFormat).WithMessage("timeZone");

        RuleFor(l => l.Request.LocationAddress)
            .MustBeValueObject(address =>
                address.Flat.HasValue
                    ? Address.CreateWithFlat(
                        address.City,
                        address.Street,
                        address.House,
                        address.Flat.Value)
                    : Address.Create(
                        address.City,
                        address.Street,
                        address.House)
            );
    }
    private bool BeValidTimeZoneFormat(string timeZone)
    {
        if (string.IsNullOrWhiteSpace(timeZone))
            return false;

        var parts = timeZone.Split('/');
        return parts.Length >= 2 &&
               !string.IsNullOrWhiteSpace(parts[0]) &&
               !string.IsNullOrWhiteSpace(parts[1]);
    }
}
