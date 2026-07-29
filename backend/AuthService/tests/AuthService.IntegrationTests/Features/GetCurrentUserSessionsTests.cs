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
using SharedService.SharedKernel;

namespace AuthService.IntegrationTests.Features;

public sealed class GetCurrentUserSessionsTests : AuthServiceBaseTests
{
    public GetCurrentUserSessionsTests(AuthServiceTestWebFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetCurrentUserSessions_With_Authenticated_User_Should_Return_Only_Current_User_Active_Sessions()
    {
        // Arrange
        ApplicationUser currentUser = await CreateIdentityUserAsync(
            "sessions-viewer@example.com",
            "sessionsviewer",
            "Sessions Viewer",
            Guid.NewGuid(),
            AuthRoles.VIEWER);

        ApplicationUser otherUser = await CreateIdentityUserAsync(
            "sessions-other@example.com",
            "sessionsother",
            "Sessions Other",
            Guid.NewGuid(),
            AuthRoles.VIEWER);

        TokenResponse login = await LoginAsync("sessions-viewer@example.com", "SessionsTest/Access");
        DateTimeOffset firstSessionLastUsedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        OpenIddictTestSession firstSession =
            await OpenIddictSessionTestHelper.CreateSessionAsync(
                Services,
                currentUser.Id,
                lastUsedAt: firstSessionLastUsedAt);
        OpenIddictTestSession secondSession =
            await OpenIddictSessionTestHelper.CreateSessionAsync(Services, currentUser.Id);
        await OpenIddictSessionTestHelper.CreateSessionAsync(
            Services,
            currentUser.Id,
            revoked: true);
        await OpenIddictSessionTestHelper.CreateSessionAsync(
            Services,
            currentUser.Id,
            expirationDate: DateTimeOffset.UtcNow.AddMinutes(-1));
        await OpenIddictSessionTestHelper.CreateSessionAsync(Services, otherUser.Id);

        using HttpRequestMessage request = new(HttpMethod.Get, "/api/auth/sessions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        // Act
        HttpResponseMessage response = await AppHttpClient.SendAsync(request);
        Envelope<IReadOnlyList<AuthSessionResponse>>? envelope =
            await response.Content.ReadFromJsonAsync<Envelope<IReadOnlyList<AuthSessionResponse>>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        envelope.Should().NotBeNull();
        envelope!.Result.Should().NotBeNull();

        IReadOnlyList<AuthSessionResponse> sessions = envelope.Result!;

        sessions.Should().HaveCount(2);
        Guid[] expectedSessionIds =
        {
            firstSession.AuthorizationId,
            secondSession.AuthorizationId
        };
        sessions.Select(session => session.Id)
            .Should()
            .BeEquivalentTo(expectedSessionIds);
        sessions.Should().OnlyContain(session => session.ExpiresAt > DateTime.UtcNow);
        sessions.Should().OnlyContain(session => session.Id != Guid.Empty);
        sessions.Should().OnlyContain(session => session.CreatedByIp == null);
        sessions.Should().OnlyContain(session => session.UserAgent == null);
        sessions.Single(session => session.Id == firstSession.AuthorizationId)
            .LastUsedAt
            .Should()
            .BeCloseTo(firstSessionLastUsedAt.UtcDateTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetCurrentUserSessions_Without_Access_Token_Should_Return_Unauthorized()
    {
        // Arrange

        // Act
        HttpResponseMessage response = await AppHttpClient.GetAsync("/api/auth/sessions");

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
