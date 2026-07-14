using FileService.Core.Grpc;

namespace FileService.Web.Configurations;

public static class GrpcExtensions
{
    public static IServiceCollection AddFileGrpc(this IServiceCollection services)
    {
        // AddGrpc включает ASP.NET Core hosting для gRPC services.
        services.AddGrpc();

        return services;
    }

    public static IEndpointRouteBuilder MapFileGrpcServices(this IEndpointRouteBuilder app)
    {
        // MapGrpcService публикует internal gRPC endpoint FileService.
        app.MapGrpcService<FileInternalGrpcService>();

        return app;
    }
}
