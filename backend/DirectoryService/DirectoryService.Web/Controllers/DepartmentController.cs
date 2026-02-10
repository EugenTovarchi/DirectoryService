using DirectoryService.Application.Commands.Departments.Create;
using DirectoryService.Application.Commands.Departments.MoveDepartment;
using DirectoryService.Application.Commands.Departments.SoftDelete;
using DirectoryService.Application.Commands.Departments.UpdateDepartmentLocations;
using DirectoryService.Application.Queries.Departments.GetDepartmentChildren;
using DirectoryService.Application.Queries.Departments.GetDepsWithChildren;
using DirectoryService.Application.Queries.Departments.GetTopByPositions;
using DirectoryService.Contracts.Requests.Departments;
using Microsoft.AspNetCore.Mvc;
using SharedService.Framework;
using SharedService.Framework.ControllersResults;

namespace DirectoryService.Web.Controllers;

public class DepartmentController : ApplicationController
{
    [HttpPost("api/departments")]
    public async Task<IActionResult> Create(
       [FromBody] CreateDepartmentRequest request,
       [FromServices] CreateDepartmentHandler handler,
       CancellationToken cancellationToken)
    {
        var command = new CreateDepartmentCommand(request);

        var result = await handler.Handle(command, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToResponse();

        return result.IsFailure ? result.Error.ToResponse() : Ok(result.Value);
    }

    [HttpPatch("api/departments/{departmentId:guid}/locations")]
    public async Task<IActionResult> UpdateLocations(
       [FromRoute] Guid departmentId,
       [FromBody] UpdateDepartmentLocationsRequest request,
       [FromServices] UpdateDepartmentLocationsHandler handler,
       CancellationToken cancellationToken)
    {
        var command = new UpdateDepartmentLocationsCommand(departmentId, request);

        var result = await handler.Handle(command, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToResponse();

        return result.IsFailure ? result.Error.ToResponse() : Ok(result.Value);
    }

    [HttpPut("api/departments/{departmentId}/parent")]
    public async Task<IActionResult> MoveDepartment(
       [FromRoute] Guid departmentId,
       [FromBody] MoveDepartmentRequest request,
       [FromServices] MoveDepartmentHandler handler,
       CancellationToken cancellationToken)
    {
        var command = new MoveDepartmentCommand(departmentId, request);

        var result = await handler.Handle(command, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToResponse();

        return result.IsFailure ? result.Error.ToResponse() : Ok(result.Value);
    }

    [HttpGet("api/departments/top-positions")]
    public async Task<IActionResult> GetByFilters(
       [FromQuery] GetDepartmentsRequest request,
       [FromServices] GetTopByPositionsDepartmentsHandler handler,
       CancellationToken cancellationToken)
    {
        var query = request.ToQuery();

        var result = await handler.Handle(query, cancellationToken);

        return Ok(result);
    }

    [HttpGet("/api/departments/roots")]
    public async Task<IActionResult> GetDepartmentsWithChildren(
       [FromQuery] GetDepartmentsWithChildrenRequest request,
       [FromServices] GetDepartmentsWithChildrenHandler handler,
       CancellationToken cancellationToken)
    {
        var query = request.ToQuery();

        var result = await handler.Handle(query, cancellationToken);

        return Ok(result);
    }

    [HttpGet("/api/departments/{parentId:guid}/children")]
    public async Task<IActionResult> GetDepartmentChildren(
       [FromRoute] Guid parentId,
       [FromQuery] GetDepartmentChildrenRequest request,
       [FromServices] GetDepartmentChildrenHandler handler,
       CancellationToken cancellationToken)
    {
        var query = new GetDepartmentChildrenQuery(parentId, request);
        var result = await handler.Handle(query, cancellationToken);

        return Ok(result);
    }

    [HttpDelete("/api/departments/soft/{departmentId:guid}")]
    public async Task<IActionResult> SoftDelete(
        [FromRoute] Guid departmentId,
        [FromServices] SoftDeleteHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new SoftDeleteCommand(departmentId);
        var result = await handler.Handle(command, cancellationToken);

        return result.IsFailure ? result.Error.ToResponse() : Ok(result.Value);
    }
}