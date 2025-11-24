using DirectoryService.Application.Commands.Locations.CreateLocation;
using DirectoryService.Contracts.Requests.Locations;
using DirectoryService.Core.Abstractions;
using DirectoryService.Framework.ControllersResults;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryService.Web.Controllers;

public class LocationController : ApplicationController
{
    [HttpPost("api/locations")]
    public async Task<IActionResult> Create(
       [FromBody] CreateLocationRequest request,
       [FromServices] CreateLocationHandler handler,
       CancellationToken cancellationToken)
    {
        var command = new CreateLocationCommand(request);

        var result = await handler.Handle(command, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToResponse();

        return Ok(result.Value);
    }
}
