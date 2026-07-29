using System.Text.Json.Serialization;

namespace AuthService.Web.Features.Oidc.UserInfo;

/// <summary>
/// Стандартные и AuthService-specific claims, возвращаемые UserInfo endpoint.
/// Null properties не сериализуются, если client не запросил соответствующий scope.
/// </summary>
public sealed class OidcUserInfoResponse
{
    [JsonPropertyName("sub")]
    public required string Subject { get; init; }

    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; init; }

    [JsonPropertyName("preferred_username")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PreferredUsername { get; init; }

    [JsonPropertyName("email")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Email { get; init; }

    [JsonPropertyName("email_verified")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? EmailVerified { get; init; }

    [JsonPropertyName("company_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CompanyId { get; init; }

    [JsonPropertyName("roles")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyCollection<string>? Roles { get; init; }

    [JsonPropertyName("permissions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyCollection<string>? Permissions { get; init; }
}
