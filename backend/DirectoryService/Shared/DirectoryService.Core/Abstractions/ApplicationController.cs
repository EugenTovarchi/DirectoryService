using DirectoryService.SharedKernel;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryService.Core.Abstractions;

[ApiController]
[Route("[controller]")]

public abstract class ApplicationController : ControllerBase
{
    public override OkObjectResult Ok(object? value)
    {
        var envelope = Envelope.Ok(value);
        return new(envelope);
    }
}
