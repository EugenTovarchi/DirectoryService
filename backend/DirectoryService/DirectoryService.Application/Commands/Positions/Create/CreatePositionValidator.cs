using DirectoryService.Contracts.ValueObjects;
using DirectoryService.Core.Validation;
using DirectoryService.SharedKernel;
using FluentValidation;

namespace DirectoryService.Application.Commands.Positions.Create;

public class CreatePositionValidator : AbstractValidator<CreatePositionCommand>
{
    public CreatePositionValidator()
    {
        RuleFor(d => d.Request.PositionName)
         .NotEmpty().WithError(Errors.General.ValueIsEmptyOrWhiteSpace("PositionName"))
         .MaximumLength(Name.MAX_LENGTH).WithError(Errors.Validation.RecordIsInvalid("PositionName"))
         .MinimumLength(Name.MIN_LENGTH).WithError(Errors.Validation.RecordIsInvalid("PositionName"));

        RuleFor(d => d.Request.DepartmentIds).NotEmpty().WithError(Errors.General.NotFoundValue("DepartmentIds"))
            .Must(ids => ids.Any()).WithError(Errors.General.ValueIsEmpty("DepartmentIds"));
    }
}
