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

public sealed class LogoutTests : AuthServiceBaseTests
{
    public LogoutTests(AuthServiceTestWebFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Logout_With_Valid_Refresh_Token_Should_Revoke_Session()
    {
        await CreateIdentityUserAsync(
            "logout-viewer@example.com",
            "logoutviewer",
            "Logout Viewer",
            Guid.NewGuid(),
            AuthRoles.VIEWER);

        var loginResponse = await LoginAsync("logout-viewer@example.com");

        var logoutResponse = await AppHttpClient.PostAsJsonAsync(
            "/api/auth/logout",
            new RefreshTokenRequest(loginResponse.RefreshToken));

        logoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var savedToken = await ExecuteInDb(dbContext => dbContext.RefreshTokens.SingleAsync());
        savedToken.RevokedAt.Should().NotBeNull();
        savedToken.ReplacedByTokenId.Should().BeNull();

        var refreshResponse = await AppHttpClient.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequest(loginResponse.RefreshToken));

        refreshResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Logout_With_Unknown_Refresh_Token_Should_Return_Ok()
    {
        var response = await AppHttpClient.PostAsJsonAsync(
            "/api/auth/logout",
            new RefreshTokenRequest("unknown-refresh-token"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        int tokenCount = await ExecuteInDb(dbContext => dbContext.RefreshTokens.CountAsync());
        tokenCount.Should().Be(0);
    }

    [Fact]
    public async Task Logout_With_Already_Revoked_Refresh_Token_Should_Return_Ok()
    {
        await CreateIdentityUserAsync(
            "logout-revoked-viewer@example.com",
            "logoutrevokedviewer",
            "Logout Revoked Viewer",
            Guid.NewGuid(),
            AuthRoles.VIEWER);

        var loginResponse = await LoginAsync("logout-revoked-viewer@example.com");

        var firstLogoutResponse = await AppHttpClient.PostAsJsonAsync(
            "/api/auth/logout",
            new RefreshTokenRequest(loginResponse.RefreshToken));
        firstLogoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondLogoutResponse = await AppHttpClient.PostAsJsonAsync(
            "/api/auth/logout",
            new RefreshTokenRequest(loginResponse.RefreshToken));

        secondLogoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        int tokenCount = await ExecuteInDb(dbContext => dbContext.RefreshTokens.CountAsync());
        tokenCount.Should().Be(1);
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
