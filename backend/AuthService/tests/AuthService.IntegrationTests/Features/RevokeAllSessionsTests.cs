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
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
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
        // Arrange
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

        OidcTestToken login = await LoginAsync("revoke-all-viewer@example.com");
        OpenIddictTestSession firstSession =
            await OpenIddictSessionTestHelper.CreateSessionAsync(Services, currentUser.Id);
        OpenIddictTestSession secondSession =
            await OpenIddictSessionTestHelper.CreateSessionAsync(Services, currentUser.Id);
        OpenIddictTestSession otherSession =
            await OpenIddictSessionTestHelper.CreateSessionAsync(Services, otherUser.Id);

        using HttpRequestMessage request = new(HttpMethod.Post, "/api/auth/revoke-all-sessions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Act
        HttpResponseMessage response = await AppHttpClient.SendAsync(request);
        OpenIddictTestSessionStatus firstStatus =
            await OpenIddictSessionTestHelper.GetStatusAsync(Services, firstSession);
        OpenIddictTestSessionStatus secondStatus =
            await OpenIddictSessionTestHelper.GetStatusAsync(Services, secondSession);
        OpenIddictTestSessionStatus otherStatus =
            await OpenIddictSessionTestHelper.GetStatusAsync(Services, otherSession);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        firstStatus.AuthorizationStatus.Should().Be(OpenIddictConstants.Statuses.Revoked);
        firstStatus.RefreshTokenStatus.Should().Be(OpenIddictConstants.Statuses.Revoked);
        secondStatus.AuthorizationStatus.Should().Be(OpenIddictConstants.Statuses.Revoked);
        secondStatus.RefreshTokenStatus.Should().Be(OpenIddictConstants.Statuses.Revoked);
        otherStatus.AuthorizationStatus.Should().Be(OpenIddictConstants.Statuses.Valid);
        otherStatus.RefreshTokenStatus.Should().Be(OpenIddictConstants.Statuses.Valid);
    }

    [Fact]
    public async Task RevokeAllSessions_Without_Access_Token_Should_Return_Unauthorized()
    {
        // Arrange

        // Act
        HttpResponseMessage response = await AppHttpClient.PostAsync(
            "/api/auth/revoke-all-sessions",
            content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private Task<OidcTestToken> LoginAsync(string email)
    {
        return LoginWithOidcAsync(email);
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
