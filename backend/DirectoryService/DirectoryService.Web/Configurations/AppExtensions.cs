using Serilog;
using SharedService.Framework.Middlewares;

namespace DirectoryService.Web.Configurations;

public static class AppExtensions
{
    public static IApplicationBuilder WebConfigure (this WebApplication app)
    {
        app.UseRouting();
        app.UseExceptionMiddleware();
        app.UseRequestCorrelationId();

        app.UseAuthorization();
        app.MapControllers();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Directory Service v1");
                c.RoutePrefix = "swagger";
            });
        }

        app.UseSerilogRequestLogging();

        return app;
    }
}
