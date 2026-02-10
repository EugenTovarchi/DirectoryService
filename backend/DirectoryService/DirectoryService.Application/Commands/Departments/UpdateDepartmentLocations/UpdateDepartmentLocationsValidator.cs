using DirectoryService.Core.Validation;
using DirectoryService.SharedKernel;
using FluentValidation;

namespace DirectoryService.Application.Commands.Departments.UpdateDepartmentLocations;

public class UpdateDepartmentLocationsValidator : AbstractValidator<UpdateDepartmentLocationsCommand>
{
    public UpdateDepartmentLocationsValidator()
    {
        RuleFor(d => d.DepartmentId).NotEmpty().WithError(Errors.General.NotFoundValue("departmentId"));

        RuleFor(d => d.Request.LocationIds).NotEmpty().WithError(Errors.General.NotFoundValue("LocationId"))
            .Must(ids => ids.Any()).WithError(Errors.General.ValueIsEmpty("LocationIds"));
    }
}
