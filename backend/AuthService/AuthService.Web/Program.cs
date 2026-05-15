using AuthService.Core;
using AuthService.Infrastructure.Postgres;
using AuthService.Web.Configurations;
using Serilog;

namespace AuthService.Web;

public partial class Program
{
    protected Program() { }

    public static async Task Main(string[] args)
    {
        try
        {
            var builder = WebApplication.CreateBuilder(args);

            string environment = builder.Environment.EnvironmentName;

            builder.Configuration.AddAuthServiceConfiguration<Program>(
                environment,
                builder.Environment);

            builder.Services.AddControllers();

            builder.Services.AddEndpointsApiExplorer();

            builder.Services.AddConfiguration(builder.Configuration);

            // Identity создает служебные токены через Data Protection: reset password, email confirmation и похожие flows.
            builder.Services.AddDataProtection();

            builder.Services.AddAuthorization();

            builder.Services.AddCore()
                .AddPostgresInfrastructure(builder.Configuration);

            var app = builder.Build();

            await app.ApplyMigrationsIfNeeded();

            app.WebConfigure();

            await app.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unhandled exception");
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }
}
