using CSharpFunctionalExtensions;
using DirectoryService.SharedKernel;
using DirectoryService.SharedKernel.ValueObjects.Ids;

namespace DirectoryService.Domain.Entities;

public class DepartmentPosition 
{
    private DepartmentPosition() { }
    private DepartmentPosition(
        PositionId positionId,
        DepartmentId departmentId)
    {
        PositionId = positionId;
        DepartmentId = departmentId;
    }
    public Guid PositionId { get; private set; }
    public Guid DepartmentId { get; private set; }

    public static Result<DepartmentPosition, Error> Create(PositionId positionId, DepartmentId departmentId)
    {
        if (positionId is null)
            return Errors.General.ValueIsEmpty("positionId");

        if (departmentId is null)
            return Errors.General.ValueIsEmpty("departmentId");

        var departmentPosition = new DepartmentPosition(positionId, departmentId);

        return departmentPosition;
    }
}
