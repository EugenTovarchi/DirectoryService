using System.Security.Authentication;
using SharedService.SharedKernel;
using SharedService.SharedKernel.Exceptions;

namespace DirectoryService.Web.Middlewares;

public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        logger.LogError(exception, "Exception was thrown in DirectoryService!");

        (int statusCode, Error error) = exception switch
        {
            NotFoundException ex => (StatusCodes.Status404NotFound, ex.Error),
            ValidationException ex => (StatusCodes.Status400BadRequest, ex.Error),
            ConflictException ex => (StatusCodes.Status409Conflict, ex.Error),
            FailureException ex => (StatusCodes.Status500InternalServerError, ex.Error),
            AuthenticationException => (StatusCodes.Status401Unauthorized, Error.Failure("authentication.failed", exception.Message)),
            _ => (StatusCodes.Status500InternalServerError, Error.Failure("server.internal", exception.Message))
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
