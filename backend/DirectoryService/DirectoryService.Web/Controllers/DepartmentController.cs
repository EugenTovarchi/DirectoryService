using DirectoryService.Contracts.Requests.Departments;
using DirectoryService.Framework.ControllersResults;
using DirectoryService.Core.Abstractions;
using Microsoft.AspNetCore.Mvc;
using DirectoryService.Application.Commands.Departments.Create;
using DirectoryService.Application.Commands.Departments.UpdateDepartmentLocations;

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
}