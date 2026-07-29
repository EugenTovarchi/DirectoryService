using System.Net;
using System.Text.Json;
using AuthService.Domain.Identity;
using AuthService.Infrastructure.Postgres.Seeding;
using AuthService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using OpenIddict.Abstractions;

namespace AuthService.IntegrationTests.Features;

public sealed class OidcClientCredentialsFlowTests : AuthServiceBaseTests
{
    public OidcClientCredentialsFlowTests(AuthServiceTestWebFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task ClientCredentials_With_Valid_Client_Should_Return_Service_AccessToken()
    {
        // Arrange
        await SeedOidcAsync();
        var tokenForm = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = "directory-service",
            ["client_secret"] = "test-directory-service-client-secret-value",
            ["scope"] = "files"
        };

        // Act
        using HttpResponseMessage response = await AppHttpClient.PostAsync(
            "/connect/token",
            new FormUrlEncodedContent(tokenForm));
        using JsonDocument responseDocument = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        string accessToken = responseDocument.RootElement
            .GetProperty("access_token")
            .GetString()
            ?? throw new InvalidOperationException("Access token is missing");
        var jwt = new JsonWebToken(accessToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        responseDocument.RootElement.GetProperty("token_type").GetString()
            .Should().Be("Bearer");
        responseDocument.RootElement.GetProperty("expires_in").GetInt32()
            .Should().BePositive();
        responseDocument.RootElement.TryGetProperty("refresh_token", out _)
            .Should().BeFalse();
        responseDocument.RootElement.TryGetProperty("id_token", out _)
            .Should().BeFalse();

        jwt.Subject.Should().Be("directory-service");
        jwt.Audiences.Should().ContainSingle("file-service");
        jwt.Claims.Should().Contain(claim =>
            claim.Type == AuthClaimTypes.CLIENT_ID &&
            claim.Value == "directory-service");
        jwt.Claims.Should().Contain(claim =>
            claim.Type == AuthClaimTypes.SERVICE_NAME &&
            claim.Value == "DirectoryService");
        jwt.Claims.Should().Contain(claim =>
            claim.Type == AuthClaimTypes.SERVICE_PERMISSION &&
            claim.Value == "file-service.internal");
        jwt.Claims.Should().Contain(claim =>
            claim.Type == OpenIddictConstants.Claims.Scope &&
            claim.Value == "files");
        jwt.Claims.Should().NotContain(claim =>
            claim.Type == AuthClaimTypes.PERMISSION ||
            claim.Type == OpenIddictConstants.Claims.Role ||
            claim.Type == AuthClaimTypes.COMPANY_ID);
    }

    [Fact]
    public async Task ClientCredentials_With_Invalid_Secret_Should_Return_InvalidClient()
    {
        // Arrange
        await SeedOidcAsync();
        var tokenForm = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = "directory-service",
            ["client_secret"] = "wrong-test-directory-service-client-secret",
            ["scope"] = "files"
        };

        // Act
        using HttpResponseMessage response = await AppHttpClient.PostAsync(
            "/connect/token",
            new FormUrlEncodedContent(tokenForm));
        using JsonDocument responseDocument = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        responseDocument.RootElement.GetProperty("error").GetString()
            .Should().Be(OpenIddictConstants.Errors.InvalidClient);
        responseDocument.RootElement.TryGetProperty("access_token", out _)
            .Should().BeFalse();
    }

    [Fact]
    public async Task ClientCredentials_With_Disallowed_Scope_Should_Return_InvalidScope()
    {
        // Arrange
        await SeedOidcAsync();
        var tokenForm = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = "directory-service",
            ["client_secret"] = "test-directory-service-client-secret-value",
            ["scope"] = "directory"
        };

        // Act
        using HttpResponseMessage response = await AppHttpClient.PostAsync(
            "/connect/token",
            new FormUrlEncodedContent(tokenForm));
        using JsonDocument responseDocument = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        responseDocument.RootElement.GetProperty("error").GetString()
            .Should().Be(OpenIddictConstants.Errors.InvalidRequest);
        responseDocument.RootElement.TryGetProperty("access_token", out _)
            .Should().BeFalse();
    }

    private async Task SeedOidcAsync()
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<OidcServerSeeder>();
        await seeder.SeedAsync();
    }
}
