using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using AuthService.Contracts.Requests;
using AuthService.Contracts.Responses;
using AuthService.Domain.Identity;
using AuthService.Infrastructure.Postgres.Seeding;
using AuthService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedService.SharedKernel;

namespace AuthService.IntegrationTests.Features;

public sealed class RefreshTokenTests : AuthServiceBaseTests
{
    public RefreshTokenTests(AuthServiceTestWebFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Refresh_With_Valid_Token_Should_Rotate_Refresh_Token()
    {
        var user = await CreateIdentityUserAsync(
            "refresh-viewer@example.com",
            "refreshviewer",
            "Refresh Viewer",
            Guid.NewGuid(),
            AuthRoles.VIEWER);

        var loginResponse = await LoginAsync("refresh-viewer@example.com");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh")
        {
            Content = JsonContent.Create(new RefreshTokenRequest(loginResponse.RefreshToken))
        };
        request.Headers.UserAgent.ParseAdd("AuthServiceIntegrationTests/2.0");

        var response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<TokenResponse>>();
        var refreshedTokens = envelope?.Result;

        refreshedTokens.Should().NotBeNull();
        refreshedTokens!.AccessToken.Should().NotBeNullOrWhiteSpace();
        refreshedTokens.RefreshToken.Should().NotBeNullOrWhiteSpace();
        refreshedTokens.RefreshToken.Should().NotBe(loginResponse.RefreshToken);
        refreshedTokens.AccessTokenExpiresAt.Should().BeAfter(DateTime.UtcNow);
        refreshedTokens.RefreshTokenExpiresAt.Should().BeAfter(DateTime.UtcNow);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(refreshedTokens.AccessToken);
        jwt.Claims.Should().Contain(claim => claim.Type == JwtRegisteredClaimNames.Sub && claim.Value == user.Id.ToString());
        jwt.Claims.Should().Contain(claim => claim.Type == AuthClaimTypes.ROLE && claim.Value == AuthRoles.VIEWER);

        var savedTokens = await ExecuteInDb(dbContext => dbContext.RefreshTokens
            .OrderBy(token => token.CreatedAt)
            .ToListAsync());

        savedTokens.Should().HaveCount(2);

        var originalToken = savedTokens[0];
        var replacementToken = savedTokens[1];

        originalToken.UserId.Should().Be(user.Id);
        originalToken.RevokedAt.Should().NotBeNull();
        originalToken.LastUsedAt.Should().NotBeNull();
        originalToken.ReplacedByTokenId.Should().Be(replacementToken.Id);

        replacementToken.UserId.Should().Be(user.Id);
        replacementToken.RevokedAt.Should().BeNull();
        replacementToken.TokenHash.Should().NotBe(refreshedTokens.RefreshToken);
        replacementToken.TokenHash.Should().HaveLength(64);
        replacementToken.UserAgent.Should().Be("AuthServiceIntegrationTests/2.0");
    }

    [Fact]
    public async Task Refresh_With_Reused_Token_Should_Revoke_Active_User_Sessions()
    {
        await CreateIdentityUserAsync(
            "reuse-viewer@example.com",
            "reuseviewer",
            "Reuse Viewer",
            Guid.NewGuid(),
            AuthRoles.VIEWER);

        var loginResponse = await LoginAsync("reuse-viewer@example.com");

        var firstRefreshResponse = await AppHttpClient.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequest(loginResponse.RefreshToken));
        firstRefreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var reuseResponse = await AppHttpClient.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequest(loginResponse.RefreshToken));

        reuseResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var savedTokens = await ExecuteInDb(dbContext => dbContext.RefreshTokens.ToListAsync());

        savedTokens.Should().HaveCount(2);
        savedTokens.Should().OnlyContain(token => token.RevokedAt != null);
    }

    [Fact]
    public async Task Refresh_With_Unknown_Token_Should_Return_BadRequest()
    {
        var response = await AppHttpClient.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequest("unknown-refresh-token"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var tokenCount = await ExecuteInDb(dbContext => dbContext.RefreshTokens.CountAsync());
        tokenCount.Should().Be(0);
    }

    private async Task<TokenResponse> LoginAsync(string email)
    {
        var response = await AppHttpClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "password123"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<TokenResponse>>();
        envelope.Should().NotBeNull();
        envelope!.Result.Should().NotBeNull();

        return envelope.Result!;
    }

    private async Task<ApplicationUser> CreateIdentityUserAsync(
        string email,
        string username,
        string displayName,
        Guid companyId,
        string role)
    {
        await using var scope = Services.CreateAsyncScope();

        var seeder = scope.ServiceProvider.GetRequiredService<AuthIdentitySeeder>();
        await seeder.SeedAsync();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser(
            email,
            Username.Create(username).Value,
            DisplayName.Create(displayName).Value,
            companyId);

        var createResult = await userManager.CreateAsync(user, "password123");
        createResult.Succeeded.Should().BeTrue(string.Join("; ", createResult.Errors.Select(error => error.Description)));

        var roleResult = await userManager.AddToRoleAsync(user, role);
        roleResult.Succeeded.Should().BeTrue(string.Join("; ", roleResult.Errors.Select(error => error.Description)));

        return user;
    }
}
