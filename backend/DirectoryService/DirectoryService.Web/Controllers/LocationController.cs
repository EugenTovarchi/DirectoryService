using DirectoryService.Application.Commands.Locations.Create;
using DirectoryService.Application.Queries.Locations.Get;
using DirectoryService.Contracts.Requests.Locations;
using DirectoryService.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedService.Framework;
using SharedService.Framework.ControllersResults;

namespace DirectoryService.Web.Controllers;

[Route("api/locations")]
public class LocationController : ApplicationController
{
    [HttpPost]
    [Authorize(Policy = DirectoryAuthorizationPolicies.DIRECTORY_MANAGE)]
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

    [HttpGet]
    [Authorize(Policy = DirectoryAuthorizationPolicies.DIRECTORY_READ)]
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
