using DirectoryService.Core.Validation;
using DirectoryService.SharedKernel;
using DirectoryService.SharedKernel.ValueObjects;
using FluentValidation;

namespace DirectoryService.Application.Commands.Departments.Create;

public class CreateDepartmentValidator : AbstractValidator<CreateDepartmentCommand>
{
    public CreateDepartmentValidator()
    {
        RuleFor(d => d.Request.DepartmentName)
         .NotEmpty().WithError(Errors.General.ValueIsEmptyOrWhiteSpace("DepartmentName"))
         .MaximumLength(Name.MAX_LENGTH).WithError(Errors.Validation.RecordIsInvalid("DepartmentName"))
         .MinimumLength(Name.MIN_LENGTH).WithError(Errors.Validation.RecordIsInvalid("DepartmentName"));

        RuleFor(d => d.Request.Identifier)
         .NotEmpty().WithError(Errors.General.ValueIsEmptyOrWhiteSpace("Identifier"))
         .MaximumLength(Identifier.MAX_LENGTH).WithError(Errors.Validation.RecordIsInvalid("Identifier"))
         .MinimumLength(Identifier.MIN_LENGTH).WithError(Errors.Validation.RecordIsInvalid("Identifier"));

        RuleFor(d => d.Request.LocationIds).NotEmpty().WithError(Errors.General.NotFoundValue("LocationId"))
            .Must(ids => ids.Any()).WithError(Errors.General.ValueIsEmpty("LocationIds")); 
    }
}
