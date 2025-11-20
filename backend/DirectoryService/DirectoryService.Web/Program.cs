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

            builder.Configuration.AddEnvironmentVariables(); //поддержка переменных окружения

            builder.Services.AddConfiguration(builder.Configuration); 

            builder.Services.AddAuthorization();

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
