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

public sealed class RevokeAllSessionsTests : AuthServiceBaseTests
{
    public RevokeAllSessionsTests(AuthServiceTestWebFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task RevokeAllSessions_With_Authenticated_User_Should_Revoke_Current_User_Sessions()
    {
        ApplicationUser currentUser = await CreateIdentityUserAsync(
            "revoke-all-viewer@example.com",
            "revokeallviewer",
            "Revoke All Viewer",
            Guid.NewGuid(),
            AuthRoles.VIEWER);

        ApplicationUser otherUser = await CreateIdentityUserAsync(
            "revoke-all-other@example.com",
            "revokeallother",
            "Revoke All Other",
            Guid.NewGuid(),
            AuthRoles.VIEWER);

        TokenResponse firstLogin = await LoginAsync("revoke-all-viewer@example.com");
        TokenResponse secondLogin = await LoginAsync("revoke-all-viewer@example.com");
        await LoginAsync("revoke-all-other@example.com");

        using HttpRequestMessage request = new(HttpMethod.Post, "/api/auth/revoke-all-sessions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", firstLogin.AccessToken);

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        List<RefreshToken> savedTokens = await ExecuteInDb(dbContext => dbContext.RefreshTokens.ToListAsync());

        savedTokens
            .Where(token => token.UserId == currentUser.Id)
            .Should()
            .OnlyContain(token => token.RevokedAt != null);

        savedTokens
            .Where(token => token.UserId == otherUser.Id)
            .Should()
            .OnlyContain(token => token.RevokedAt == null);

        HttpResponseMessage firstRefreshResponse = await AppHttpClient.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequest(firstLogin.RefreshToken));
        firstRefreshResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        HttpResponseMessage secondRefreshResponse = await AppHttpClient.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequest(secondLogin.RefreshToken));
        secondRefreshResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RevokeAllSessions_Without_Access_Token_Should_Return_Unauthorized()
    {
        HttpResponseMessage response = await AppHttpClient.PostAsync(
            "/api/auth/revoke-all-sessions",
            content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<TokenResponse> LoginAsync(string email)
    {
        HttpResponseMessage response = await AppHttpClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "password123"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        Envelope<TokenResponse>? envelope = await response.Content.ReadFromJsonAsync<Envelope<TokenResponse>>();
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
