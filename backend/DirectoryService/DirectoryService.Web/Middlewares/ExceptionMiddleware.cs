using DirectoryService.SharedKernel;
using DirectoryService.SharedKernel.Exceptions;
using System.Security.Authentication;

namespace DirectoryService.Web.Middlewares;

public class ExceptionMiddleware
{
    public readonly RequestDelegate _next;
    public readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        _logger.LogError(exception, "Exception was thrown in DirectoryService!");

        (int statusCode, Error error) = exception switch
        {
            NotFoundException ex => (StatusCodes.Status404NotFound, ex.Error),
            ValidationException ex => (StatusCodes.Status400BadRequest, ex.Error),
            ConflictException ex => (StatusCodes.Status409Conflict, ex.Error),
            FailureException ex => (StatusCodes.Status500InternalServerError, ex.Error),
            AuthenticationException  => (StatusCodes.Status401Unauthorized, Error.Failure("authentication.failed", exception.Message)),
            _ => (StatusCodes.Status500InternalServerError, Error.Failure("server.interanl", exception.Message)) 
        };

        var envelope = Envelope.Error(error);
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        await context.Response.WriteAsJsonAsync(envelope);
    }
}

public static class ExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ExceptionMiddleware>();
    }
}
