using Microsoft.AspNetCore.Routing;

namespace FileService.Core.EndpointSettings;

public interface IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app);
}