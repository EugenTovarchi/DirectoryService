using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using SharedService.Framework.EndpointSettings;
using SharedService.Framework.Middlewares;

namespace FileService.Web.Configurations;

public static class AppExtensions
{
    public static IApplicationBuilder WebConfigure(this WebApplication app)
    {
        app.UseRouting();
        app.UseExceptionMiddleware();
        app.UseRequestCorrelationId();

        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(FileServiceHealthChecks.LIVE_TAG)
        }).AllowAnonymous();

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(FileServiceHealthChecks.READY_TAG)
        }).AllowAnonymous();

        if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Docker"))
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "File Service v1");
                c.RoutePrefix = "swagger";
            });
        }

        app.UseSerilogRequestLogging();

        app.MapFileGrpcServices();

        app.MapEndpoints();

        return app;
    }
}
