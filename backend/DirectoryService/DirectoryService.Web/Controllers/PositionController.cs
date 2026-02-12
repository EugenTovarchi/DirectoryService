using DirectoryService.Application.Commands.Positions.Create;
using DirectoryService.Contracts.Requests.Positions;
using Microsoft.AspNetCore.Mvc;
using SharedService.Framework;
using SharedService.Framework.ControllersResults;

namespace DirectoryService.Web.Controllers;

public class PositionController : ApplicationController
{
    [HttpPost("api/positions")]
    public async Task<IActionResult> Create(
       [FromBody] CreatePositionRequest request,
       [FromServices] CreatePositionHandler handler,
       CancellationToken cancellationToken)
    {
        var command = new CreatePositionCommand(request);

        var result = await handler.Handle(command, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToResponse();

        return result.IsFailure ? result.Error.ToResponse() : Ok(result.Value);
    }
}
