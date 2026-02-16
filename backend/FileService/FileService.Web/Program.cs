using FileService.Core;
using FileService.Core.EndpointSettings;
using FileService.Infrastructure.S3;
using FileService.Web.Configurations;
using Serilog;

namespace FileService.Web;

public class Program
{
    public static async Task Main(string[] args)
    {
        try
        {
            var builder = WebApplication.CreateBuilder(args);

            string environment = builder.Environment.EnvironmentName;

            builder.Configuration.AddJsonFile($"appsettings.{environment}.json", true, true);

            builder.Configuration.AddUserSecrets<Program>();

            builder.Configuration.AddEnvironmentVariables();

            builder.Services.AddControllers();

            builder.Services.AddEndpointsApiExplorer();

            builder.Services.AddConfiguration(builder.Configuration);

            builder.Services.AddAuthorization();

            builder.Services.AddCore()
                            .AddS3(builder.Configuration);

            var app = builder.Build();

            // await app.ApplyMigrations();
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