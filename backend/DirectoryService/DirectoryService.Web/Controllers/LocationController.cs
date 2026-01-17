using DirectoryService.Application.Commands.Locations.Create;
using DirectoryService.Application.Commands.Locations.Get;
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

        return result.IsFailure ? result.Error.ToResponse() : Ok(result.Value);
    }

    [HttpGet("api/locations")]
    public async Task<IActionResult> GetByFilters(
       [FromQuery] GetLocationsRequest request,
       [FromServices] GetLocationsHandler handler,
       CancellationToken cancellationToken)
    {
        var query = request.ToQuery();

        var result = await handler.Handle(query, cancellationToken);

        return Ok(result);
    }
}
