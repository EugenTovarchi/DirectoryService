using DirectoryService.Web.Middlewares;
using Serilog;

namespace DirectoryService.Web.Configurations;

public static class AppExtensions
{
    public static IApplicationBuilder WebConfigure (this WebApplication app)
    {
        app.UseRequestCorrelationId();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "DeviceService v1");
            });
        }

        app.UseSerilogRequestLogging();
        app.UseRequestCorrelationId();
        app.UseAuthorization();
        app.UseRouting();

        return app;
    }
}
