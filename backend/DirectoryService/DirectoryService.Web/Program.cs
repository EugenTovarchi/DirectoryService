using DirectoryService.Infrastructure.Postgres;
using DirectoryService.Web.Configurations;
using Serilog;
using System.Globalization;

namespace DirectoryService.Web;

public class Program
{
    public static void Main(string[] args)
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

            builder.Configuration.AddEnvironmentVariables(); 

            builder.Services.AddConfiguration(builder.Configuration); 

            builder.Services.AddAuthorization();

            builder.Services
                .AddDirectoryServiceInfrastructure(builder.Configuration);

            var app = builder.Build();

            app.WebConfigure();

            app.Run();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unhandled exception");
        }
        finally
        {
            Log.CloseAndFlushAsync();
        }
    }
}
