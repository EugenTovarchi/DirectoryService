using CSharpFunctionalExtensions;
using DirectoryService.Contracts.ValueObjects.Ids;
using DirectoryService.SharedKernel;

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

    public PositionId PositionId { get; private set; } = null!;
    public DepartmentId DepartmentId { get; private set; } = null!;

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
