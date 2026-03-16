using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FileService.Contracts.HttpCommunication;

public static class FileServiceExtensions
{
    public static IServiceCollection AddFileServiceHttpCommunication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FileServiceOptions>(configuration.GetSection(nameof(FileServiceOptions)));
        services.AddHttpClient<IFileCommunicationService, FileHttpClient>((sp, config) =>
        {
            FileServiceOptions options = sp.GetRequiredService<IOptions<FileServiceOptions>>().Value;
            config.BaseAddress = new Uri(options.Url);
            config.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        return services;
    }
}