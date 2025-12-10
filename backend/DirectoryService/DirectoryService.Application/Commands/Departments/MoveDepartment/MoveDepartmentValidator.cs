using DirectoryService.Core.Validation;
using DirectoryService.SharedKernel;
using FluentValidation;

namespace DirectoryService.Application.Commands.Departments.MoveDepartment;

public class MoveDepartmentValidator : AbstractValidator<MoveDepartmentCommand>
{
    public MoveDepartmentValidator()
    {
        RuleFor(d => d.DepartmentId).NotEmpty().WithError(Errors.General.NotFoundValue("DepartmentId"));
    }
}
