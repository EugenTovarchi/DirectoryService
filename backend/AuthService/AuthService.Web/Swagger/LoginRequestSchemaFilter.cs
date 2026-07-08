using System.Text.Json.Nodes;
using AuthService.Contracts.Requests;
using AuthService.Infrastructure.Postgres.Seeding;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AuthService.Web.Swagger;

public sealed class LoginRequestSchemaFilter : ISchemaFilter
{
    private readonly LocalViewerSeedOptions _seedOptions;

    public LoginRequestSchemaFilter(IOptions<LocalViewerSeedOptions> seedOptions)
    {
        _seedOptions = seedOptions.Value;
    }

    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type != typeof(LoginRequest) ||
            !_seedOptions.Enabled ||
            string.IsNullOrWhiteSpace(_seedOptions.Email))
        {
            return;
        }

        if (schema.Properties is not null &&
            schema.Properties.TryGetValue("email", out IOpenApiSchema? emailSchema) &&
            emailSchema is OpenApiSchema mutableEmailSchema)
        {
            mutableEmailSchema.Example = JsonValue.Create(_seedOptions.Email);
        }
    }
}
