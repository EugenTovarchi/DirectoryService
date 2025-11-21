using CSharpFunctionalExtensions;
using DirectoryService.SharedKernel;
using DirectoryService.SharedKernel.ValueObjects.Ids;

namespace DirectoryService.Domain.Entities;

public class DepartmentLocation
{
    private DepartmentLocation() {}

    private DepartmentLocation(LocationId locationId, DepartmentId departmentId)
    {
        LocationId = locationId;
        DepartmentId = departmentId;
    }
    public Guid LocationId { get; private set; }
    public Guid DepartmentId { get; private set; }

    public static Result<DepartmentLocation, Error>Create (LocationId locationId, DepartmentId departmentId)
    {
        if (locationId is null)
            return Errors.General.ValueIsEmpty("locationId");

        if (departmentId is null)
            return Errors.General.ValueIsEmpty("departmentId");

        var departmentLocation = new DepartmentLocation(locationId, departmentId);
        return departmentLocation;
    }
}
