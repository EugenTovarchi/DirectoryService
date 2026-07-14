using FileService.Contracts.Grpc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FileService.Contracts.HttpCommunication;

public static class FileServiceExtensions
{
    public static IServiceCollection AddFileServiceHttpCommunication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FileServiceOptions>(configuration.GetSection(nameof(FileServiceOptions)));

        services.AddGrpcClient<FileInternal.FileInternalClient>((sp, config) =>
        {
            FileServiceOptions options = sp.GetRequiredService<IOptions<FileServiceOptions>>().Value;

            // GrpcUrl отделяет внутренний gRPC endpoint от внешнего/legacy HTTP Url.
            string grpcUrl = string.IsNullOrWhiteSpace(options.GrpcUrl)
                ? options.Url
                : options.GrpcUrl;

            config.Address = new Uri(grpcUrl);
        });

        services.AddHttpClient<IServiceTokenProvider, AuthServiceTokenProvider>((sp, config) =>
        {
            FileServiceOptions options = sp.GetRequiredService<IOptions<FileServiceOptions>>().Value;

            // AuthServiceUrl нужен только для service-token flow перед internal gRPC calls.
            config.BaseAddress = new Uri(options.AuthServiceUrl);
            config.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        // Adapter скрывает gRPC transport за старым IFileCommunicationService.
        services.AddScoped<IFileCommunicationService, FileCommunicationClient>();

        return services;
    }
}
