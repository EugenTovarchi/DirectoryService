using System.Text.Json.Nodes;
using AuthService.Contracts.Requests;
using AuthService.Infrastructure.Postgres.Seeding;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AuthService.Web.Swagger;

/// <summary>
/// Подставляет email первого включённого local user в Swagger login example.
/// Password намеренно не добавляется в OpenAPI document.
/// </summary>
public sealed class LoginRequestSchemaFilter : ISchemaFilter
{
    private readonly LocalViewerSeedOptions _viewerOptions;
    private readonly LocalUsersSeedOptions _usersOptions;

    public LoginRequestSchemaFilter(
        IOptions<LocalViewerSeedOptions> viewerOptions,
        IOptions<LocalUsersSeedOptions> usersOptions)
    {
        _viewerOptions = viewerOptions.Value;
        _usersOptions = usersOptions.Value;
    }

    /// <summary>
    /// Меняет только email example для LoginRequest и не затрагивает schema других requests.
    /// </summary>
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type != typeof(LoginRequest))
        {
            return;
        }

        string? email = FindExampleEmail();
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        if (schema.Properties is not null &&
            schema.Properties.TryGetValue("email", out IOpenApiSchema? emailSchema) &&
            emailSchema is OpenApiSchema mutableEmailSchema)
        {
            mutableEmailSchema.Example = JsonValue.Create(email);
        }
    }

    private string? FindExampleEmail()
    {
        if (_usersOptions.Enabled)
        {
            LocalUserSeedDefinition? user = _usersOptions.Users.FirstOrDefault(
                configuredUser => !string.IsNullOrWhiteSpace(configuredUser.Email));
            if (user is not null)
            {
                return user.Email;
            }
        }

        if (_viewerOptions.Enabled &&
            !string.IsNullOrWhiteSpace(_viewerOptions.Email))
        {
            return _viewerOptions.Email;
        }

        return null;
    }
}
