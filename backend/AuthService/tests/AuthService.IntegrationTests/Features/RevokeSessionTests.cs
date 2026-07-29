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

public sealed class RevokeSessionTests : AuthServiceBaseTests
{
    public RevokeSessionTests(AuthServiceTestWebFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task RevokeSession_With_Current_User_Session_Should_Revoke_Only_That_Session()
    {
        // Arrange
        ApplicationUser user = await CreateIdentityUserAsync(
            "revoke-session-viewer@example.com",
            "revokesessionviewer",
            "Revoke Session Viewer",
            Guid.NewGuid(),
            AuthRoles.VIEWER);

        TokenResponse login = await LoginAsync(
            "revoke-session-viewer@example.com",
            "RevokeSession/Access");
        OpenIddictTestSession preservedSession =
            await OpenIddictSessionTestHelper.CreateSessionAsync(Services, user.Id);
        OpenIddictTestSession revokedSession =
            await OpenIddictSessionTestHelper.CreateSessionAsync(Services, user.Id);

        using HttpRequestMessage request = new(HttpMethod.Post, "/api/auth/revoke-session")
        {
            Content = JsonContent.Create(
                new RevokeSessionRequest(revokedSession.AuthorizationId))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Act
        HttpResponseMessage response = await AppHttpClient.SendAsync(request);
        OpenIddictTestSessionStatus revokedStatus =
            await OpenIddictSessionTestHelper.GetStatusAsync(Services, revokedSession);
        OpenIddictTestSessionStatus preservedStatus =
            await OpenIddictSessionTestHelper.GetStatusAsync(Services, preservedSession);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        revokedStatus.AuthorizationStatus.Should().Be(OpenIddictConstants.Statuses.Revoked);
        revokedStatus.RefreshTokenStatus.Should().Be(OpenIddictConstants.Statuses.Revoked);
        preservedStatus.AuthorizationStatus.Should().Be(OpenIddictConstants.Statuses.Valid);
        preservedStatus.RefreshTokenStatus.Should().Be(OpenIddictConstants.Statuses.Valid);
    }

    [Fact]
    public async Task RevokeSession_With_Other_User_Session_Should_Return_Ok_And_Not_Revoke_It()
    {
        // Arrange
        ApplicationUser currentUser = await CreateIdentityUserAsync(
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

        TokenResponse currentLogin = await LoginAsync(
            "revoke-session-current@example.com",
            "RevokeSession/Current");
        await OpenIddictSessionTestHelper.CreateSessionAsync(Services, currentUser.Id);
        OpenIddictTestSession otherSession =
            await OpenIddictSessionTestHelper.CreateSessionAsync(Services, otherUser.Id);

        using HttpRequestMessage request = new(HttpMethod.Post, "/api/auth/revoke-session")
        {
            Content = JsonContent.Create(
                new RevokeSessionRequest(otherSession.AuthorizationId))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", currentLogin.AccessToken);

        // Act
        HttpResponseMessage response = await AppHttpClient.SendAsync(request);
        OpenIddictTestSessionStatus otherSessionStatus =
            await OpenIddictSessionTestHelper.GetStatusAsync(Services, otherSession);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        otherSessionStatus.AuthorizationStatus.Should().Be(OpenIddictConstants.Statuses.Valid);
        otherSessionStatus.RefreshTokenStatus.Should().Be(OpenIddictConstants.Statuses.Valid);
    }

    [Fact]
    public async Task RevokeSession_Without_Access_Token_Should_Return_Unauthorized()
    {
        // Arrange

        // Act
        HttpResponseMessage response = await AppHttpClient.PostAsJsonAsync(
            "/api/auth/revoke-session",
            new RevokeSessionRequest(Guid.NewGuid()));

        // Assert
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
