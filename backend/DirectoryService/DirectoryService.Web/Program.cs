using System.Globalization;
using DirectoryService.Application;
using DirectoryService.Infrastructure.Postgres;
using DirectoryService.Web.Configurations;
using Serilog;

namespace DirectoryService.Web;

public class Program
{
    public async static Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
            .CreateLogger();

        try
        {
            var builder = WebApplication.CreateBuilder(args);

            string environment = builder.Environment.EnvironmentName;

            builder.Configuration.AddJsonFile($"appsettings.{environment}.json", true, true);

            builder.Configuration.AddUserSecrets<Program>();

            builder.Services.AddControllers();

            builder.Configuration.AddEnvironmentVariables();

            builder.Services.AddConfiguration(builder.Configuration);

            builder.Services.AddAuthorization();

            builder.Services.AddDirectoryServiceInfrastructure(builder.Configuration)
                            .AddDirectoryServiceApplication(builder.Configuration);

            var app = builder.Build();

            await app.ApplyMigrations();

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
