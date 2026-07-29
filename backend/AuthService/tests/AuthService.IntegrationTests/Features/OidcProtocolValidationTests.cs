using System.Net;
using AuthService.Infrastructure.Postgres.Seeding;
using AuthService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace AuthService.IntegrationTests.Features;

public sealed class OidcProtocolValidationTests : AuthServiceBaseTests
{
    public OidcProtocolValidationTests(AuthServiceTestWebFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Authorize_With_Unregistered_RedirectUri_Should_Return_BadRequest()
    {
        // Arrange
        await SeedOidcAsync();
        string url = CreateAuthorizationUrl(
            "https://attacker.example/callback",
            "openid profile");

        // Act
        using HttpResponseMessage response = await AppHttpClient.GetAsync(url);
        string body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().Contain("invalid_request");
    }

    [Fact]
    public async Task Authorize_With_Unknown_Client_Should_Return_BadRequest()
    {
        // Arrange
        await SeedOidcAsync();
        string url = CreateAuthorizationUrl(
            clientId: "unknown-client",
            redirectUri: "https://public-client.tests/callback",
            scope: "openid profile");

        // Act
        using HttpResponseMessage response = await AppHttpClient.GetAsync(url);
        string body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().Contain("invalid_request");
    }

    [Fact]
    public async Task Authorize_With_Unsupported_Scope_Should_Redirect_Protocol_Error()
    {
        // Arrange
        await SeedOidcAsync();
        string url = CreateAuthorizationUrl(
            "https://public-client.tests/callback",
            "openid unsupported");

        // Act
        using HttpResponseMessage response = await AppHttpClient.GetAsync(url);
        string body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().Contain("invalid_scope");
    }

    [Fact]
    public async Task UserInfo_With_Invalid_AccessToken_Should_Return_Unauthorized()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Get, "/connect/userinfo");
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid-token");

        // Act
        using HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static string CreateAuthorizationUrl(
        string redirectUri,
        string scope,
        string clientId = "oidc-public-tests")
    {
        var parameters = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = scope,
            ["code_challenge"] = "abcdefghijklmnopqrstuvwxyz0123456789ABCDEFG",
            ["code_challenge_method"] = "S256"
        };

        return QueryHelpers.AddQueryString("/connect/authorize", parameters);
    }

    private async Task SeedOidcAsync()
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<OidcServerSeeder>();
        await seeder.SeedAsync();
    }
}
