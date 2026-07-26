using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using AuthService.Contracts.Requests;
using AuthService.Contracts.Responses;
using AuthService.Domain.Identity;
using AuthService.IntegrationTests.Infrastructure;
using FluentAssertions;
using SharedService.SharedKernel;

namespace AuthService.IntegrationTests.Features;

public sealed class IssueServiceTokenTests : AuthServiceBaseTests
{
    public IssueServiceTokenTests(AuthServiceTestWebFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task IssueServiceToken_With_Valid_ClientCredentials_Should_Return_Service_AccessToken()
    {
        var request = new ClientCredentialsTokenRequest(
            "directory-service",
            "test-directory-service-client-secret-value");

        var response = await AppHttpClient.PostAsJsonAsync("/api/auth/service-token", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<ServiceTokenResponse>>();
        ServiceTokenResponse? tokenResponse = envelope?.Result;

        tokenResponse.Should().NotBeNull();
        tokenResponse!.AccessToken.Should().NotBeNullOrWhiteSpace();
        tokenResponse.AccessTokenExpiresAt.Should().BeAfter(DateTime.UtcNow);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(tokenResponse.AccessToken);
        jwt.Claims.Should().Contain(claim => claim.Type == JwtRegisteredClaimNames.Sub && claim.Value == "directory-service");
        jwt.Claims.Should().Contain(claim => claim.Type == AuthClaimTypes.CLIENT_ID && claim.Value == "directory-service");
        jwt.Claims.Should().Contain(claim => claim.Type == AuthClaimTypes.SERVICE_NAME && claim.Value == "DirectoryService");
        jwt.Claims.Should().Contain(claim =>
            claim.Type == AuthClaimTypes.SERVICE_PERMISSION &&
            claim.Value == "file-service.internal");
        jwt.Claims.Should().NotContain(claim => claim.Type == AuthClaimTypes.PERMISSION);
    }

    [Fact]
    public async Task IssueServiceToken_With_Invalid_ClientSecret_Should_Return_BadRequest()
    {
        var request = new ClientCredentialsTokenRequest(
            "directory-service",
            "wrong-secret");

        var response = await AppHttpClient.PostAsJsonAsync("/api/auth/service-token", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
