using System.Net;
using System.Text.Json;
using AuthService.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace AuthService.IntegrationTests.Features;

public sealed class OidcDiscoveryTests : AuthServiceBaseTests
{
    public OidcDiscoveryTests(AuthServiceTestWebFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Discovery_Should_Expose_AuthorizationCodeWithPkce_Endpoints()
    {
        // Arrange
        const string discoveryEndpoint =
            "https://auth-service.tests/.well-known/openid-configuration";

        // Act
        using HttpResponseMessage response = await AppHttpClient.GetAsync(
            discoveryEndpoint);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        JsonElement root = document.RootElement;

        root.GetProperty("issuer").GetString().Should().Be("https://auth-service.tests/");
        root.GetProperty("authorization_endpoint").GetString()
            .Should().Be("https://auth-service.tests/connect/authorize");
        root.GetProperty("token_endpoint").GetString()
            .Should().Be("https://auth-service.tests/connect/token");
        root.GetProperty("revocation_endpoint").GetString()
            .Should().Be("https://auth-service.tests/connect/revoke");
        root.GetProperty("userinfo_endpoint").GetString()
            .Should().Be("https://auth-service.tests/connect/userinfo");
        root.GetProperty("grant_types_supported")
            .EnumerateArray()
            .Select(value => value.GetString())
            .Should()
            .Contain(new[] { "authorization_code", "refresh_token" });
        root.GetProperty("code_challenge_methods_supported")
            .EnumerateArray()
            .Select(value => value.GetString())
            .Should()
            .Contain("S256");
        root.GetProperty("claims_supported")
            .EnumerateArray()
            .Select(value => value.GetString())
            .Should()
            .Contain(new[]
            {
                "sub",
                "name",
                "preferred_username",
                "email",
                "email_verified",
                "role",
                "company_id",
                "permission"
            });
    }

    [Fact]
    public async Task JsonWebKeySet_Should_Expose_Asymmetric_SigningKey()
    {
        // Arrange
        const string jsonWebKeySetEndpoint =
            "https://auth-service.tests/.well-known/jwks";

        // Act
        using HttpResponseMessage response = await AppHttpClient.GetAsync(
            jsonWebKeySetEndpoint);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        JsonElement[] keys = document.RootElement
            .GetProperty("keys")
            .EnumerateArray()
            .ToArray();

        keys.Should().ContainSingle();
        keys[0].GetProperty("use").GetString().Should().Be("sig");
        keys[0].GetProperty("kty").GetString().Should().Be("RSA");
        keys[0].GetProperty("kid").GetString().Should().NotBeNullOrWhiteSpace();
    }
}
