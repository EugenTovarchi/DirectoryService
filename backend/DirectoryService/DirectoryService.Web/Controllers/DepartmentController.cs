using DirectoryService.Application.Commands.Departments;
using DirectoryService.Contracts.Requests.Departments;
using DirectoryService.Framework.ControllersResults;
using DirectoryService.Core.Abstractions;
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
}