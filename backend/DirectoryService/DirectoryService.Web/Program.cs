using DirectoryService.Infrastructure.Postgres;
using DirectoryService.Web.Configurations;
using Serilog;
using System.Globalization;

namespace DirectoryService.Web;

public class Program
{
    public async static Task Main(string[] args)
    {

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
            .CreateBootstrapLogger();

        try
        {
            var builder = WebApplication.CreateBuilder(args);

            var environment = builder.Environment.EnvironmentName;

            builder.Configuration.AddJsonFile($"appsettings.{environment}.json", true, true);
            builder.Configuration.AddUserSecrets<Program>();

            builder.Configuration.AddEnvironmentVariables();

            builder.Services.AddConfiguration(builder.Configuration); 

            builder.Services.AddAuthorization();

            builder.Services
                .AddDirectoryServiceInfrastructure(builder.Configuration);

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
