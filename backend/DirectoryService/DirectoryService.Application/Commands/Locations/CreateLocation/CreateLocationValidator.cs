using DirectoryService.Core.Validation;
using DirectoryService.SharedKernel;
using DirectoryService.SharedKernel.ValueObjects;
using FluentValidation;
using TimeZone = DirectoryService.SharedKernel.ValueObjects.TimeZone;

namespace DirectoryService.Application.Commands.Locations.CreateLocation;

public class CreateLocationValidator : AbstractValidator<CreateLocationCommand>
{
    public CreateLocationValidator()
    {
        RuleFor(l => l.Request.LocationName)
         .NotEmpty().WithError(Errors.General.ValueIsEmptyOrWhiteSpace("LocationName"))
         .MaximumLength(Name.MAX_LENGTH).WithError(Errors.Validation.RecordIsInvalid("LocationName"))
         .MinimumLength(Name.MIN_LENGTH).WithError(Errors.Validation.RecordIsInvalid("LocationName"));

        RuleFor(l => l.Request.TimeZone)
            .NotEmpty().WithError(Errors.General.ValueIsRequired("TimeZone"))
            .MaximumLength(TimeZone.MAX_LENGTH).WithError(Errors.Validation.RecordIsInvalid("TimeZone"))
            .Must(BeValidTimeZoneFormat).WithError(Errors.Validation.RecordIsInvalid("TimeZone"));

        RuleFor(l => l.Request.LocationAddress)
            .MustBeValueObject(address =>
                address.Flat.HasValue
                    ? Address.CreateWithFlat(
                        address.Country,
                        address.City,
                        address.Street,
                        address.House,
                        address.Flat.Value)
                    : Address.Create(
                        address.Country,
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
