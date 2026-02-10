using Microsoft.Extensions.Primitives;
using Serilog.Context;

namespace DirectoryService.Web.Middlewares;

/// <summary>
/// Отслеживаем логи запроса по этому id в Seq (CorrelationId="0GHJQKRj000f:0000231")
/// По сути тоже самое что и RequestId
/// </summary>
public class RequestCorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    private const string CORRELATION_ID_HEADER_NAME = "X-Correlation-Id";
    private const string CORRELATION_ID = "Correlation-Id";

    public RequestCorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public  Task Invoke(HttpContext httpContext)
    {
        httpContext.Request.Headers.TryGetValue(CORRELATION_ID_HEADER_NAME, out StringValues correlationIdValues);

        var coreelationId = correlationIdValues.FirstOrDefault() ?? httpContext.TraceIdentifier;

        using (LogContext.PushProperty(CORRELATION_ID, coreelationId))
        {
            return _next(httpContext);
        }
    }
}

public static class RequestCorrelationIdMiddlewareExtension
{
    public static IApplicationBuilder UseRequestCorrelationId(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RequestCorrelationIdMiddleware>();
    }
}