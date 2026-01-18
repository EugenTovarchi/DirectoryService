using DirectoryService.Application.Commands.Departments.Create;
using DirectoryService.Application.Commands.Departments.GetTopByPositions;
using DirectoryService.Application.Commands.Departments.MoveDepartment;
using DirectoryService.Application.Commands.Departments.UpdateDepartmentLocations;
using DirectoryService.Application.Commands.Locations.Get;
using DirectoryService.Contracts.Requests.Departments;
using DirectoryService.Core.Abstractions;
using DirectoryService.Framework.ControllersResults;
using Microsoft.AspNetCore.Mvc;

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
        var command = new UpdateDepartmentLocationsCommand(departmentId,request);

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
}