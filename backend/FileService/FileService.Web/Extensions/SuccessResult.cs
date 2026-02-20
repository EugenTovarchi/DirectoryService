using System.Net;
using SharedService.SharedKernel;

namespace FileService.Web.Extensions;

public class SuccessResult : IResult
{
    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var envelope = Envelope.Ok();

        httpContext.Response.StatusCode = (int)HttpStatusCode.OK;

        return httpContext.Response.WriteAsJsonAsync(envelope);
    }
}

public class ControllerResult<TValue> : IResult
{
    private readonly TValue _value;

    public ControllerResult(TValue value)
    {
        _value = value;
    }

    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var envelope = Envelope.Ok();

        httpContext.Response.StatusCode = (int)HttpStatusCode.OK;

        return httpContext.Response.WriteAsJsonAsync(envelope);
    }
}
