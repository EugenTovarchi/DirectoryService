using System.Net;
using System.Net.Http.Headers;
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

public sealed class RevokeSessionTests : AuthServiceBaseTests
{
    public RevokeSessionTests(AuthServiceTestWebFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task RevokeSession_With_Current_User_Session_Should_Revoke_Only_That_Session()
    {
        ApplicationUser user = await CreateIdentityUserAsync(
            "revoke-session-viewer@example.com",
            "revokesessionviewer",
            "Revoke Session Viewer",
            Guid.NewGuid(),
            AuthRoles.VIEWER);

        TokenResponse firstLogin = await LoginAsync("revoke-session-viewer@example.com", "RevokeSession/1.0");
        await LoginAsync("revoke-session-viewer@example.com", "RevokeSession/2.0");

        Guid sessionId = await GetSessionIdByUserAgentAsync(user.Id, "RevokeSession/2.0");

        using HttpRequestMessage request = new(HttpMethod.Post, "/api/auth/revoke-session")
        {
            Content = JsonContent.Create(new RevokeSessionRequest(sessionId))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", firstLogin.AccessToken);

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        List<RefreshToken> savedTokens = await ExecuteInDb(dbContext => dbContext.RefreshTokens.ToListAsync());
        savedTokens.Single(token => token.Id == sessionId).RevokedAt.Should().NotBeNull();
        savedTokens.Single(token => string.Equals(token.UserAgent, "RevokeSession/1.0", StringComparison.Ordinal))
            .RevokedAt
            .Should()
            .BeNull();
    }

    [Fact]
    public async Task RevokeSession_With_Other_User_Session_Should_Return_Ok_And_Not_Revoke_It()
    {
        await CreateIdentityUserAsync(
            "revoke-session-current@example.com",
            "revokesessioncurrent",
            "Revoke Session Current",
            Guid.NewGuid(),
            AuthRoles.VIEWER);

        ApplicationUser otherUser = await CreateIdentityUserAsync(
            "revoke-session-other@example.com",
            "revokesessionother",
            "Revoke Session Other",
            Guid.NewGuid(),
            AuthRoles.VIEWER);

        TokenResponse currentLogin = await LoginAsync("revoke-session-current@example.com", "RevokeSession/Current");
        await LoginAsync("revoke-session-other@example.com", "RevokeSession/Other");

        Guid otherSessionId = await GetSessionIdByUserAgentAsync(otherUser.Id, "RevokeSession/Other");

        using HttpRequestMessage request = new(HttpMethod.Post, "/api/auth/revoke-session")
        {
            Content = JsonContent.Create(new RevokeSessionRequest(otherSessionId))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", currentLogin.AccessToken);

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        RefreshToken otherSession = await ExecuteInDb(dbContext => dbContext.RefreshTokens.SingleAsync(token => token.Id == otherSessionId));
        otherSession.RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task RevokeSession_Without_Access_Token_Should_Return_Unauthorized()
    {
        HttpResponseMessage response = await AppHttpClient.PostAsJsonAsync(
            "/api/auth/revoke-session",
            new RevokeSessionRequest(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<TokenResponse> LoginAsync(string email, string userAgent)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest(email, "password123"))
        };

        request.Headers.UserAgent.ParseAdd(userAgent);

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        Envelope<TokenResponse>? envelope = await response.Content.ReadFromJsonAsync<Envelope<TokenResponse>>();
        envelope.Should().NotBeNull();
        envelope!.Result.Should().NotBeNull();

        return envelope.Result!;
    }

    private Task<Guid> GetSessionIdByUserAgentAsync(Guid userId, string userAgent)
    {
        return ExecuteInDb(dbContext => dbContext.RefreshTokens
            .Where(token => token.UserId == userId && token.UserAgent == userAgent)
            .Select(token => token.Id)
            .SingleAsync());
    }

    private async Task<ApplicationUser> CreateIdentityUserAsync(
        string email,
        string username,
        string displayName,
        Guid companyId,
        string role)
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();

        AuthIdentitySeeder seeder = scope.ServiceProvider.GetRequiredService<AuthIdentitySeeder>();
        await seeder.SeedAsync();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser user = new(
            email,
            Username.Create(username).Value,
            DisplayName.Create(displayName).Value,
            companyId);

        IdentityResult createResult = await userManager.CreateAsync(user, "password123");
        createResult.Succeeded.Should().BeTrue(string.Join("; ", createResult.Errors.Select(error => error.Description)));

        IdentityResult roleResult = await userManager.AddToRoleAsync(user, role);
        roleResult.Succeeded.Should().BeTrue(string.Join("; ", roleResult.Errors.Select(error => error.Description)));

        return user;
    }
}
