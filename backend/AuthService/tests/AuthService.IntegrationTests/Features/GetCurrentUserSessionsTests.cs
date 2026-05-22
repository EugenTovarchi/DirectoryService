using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AuthService.Contracts.Requests;
using AuthService.Contracts.Responses;
using AuthService.Domain.Identity;
using AuthService.Infrastructure.Postgres;
using AuthService.Infrastructure.Postgres.Seeding;
using AuthService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
        ApplicationUser currentUser = await CreateIdentityUserAsync(
            "sessions-viewer@example.com",
            "sessionsviewer",
            "Sessions Viewer",
            Guid.NewGuid(),
            AuthRoles.VIEWER);

        await CreateIdentityUserAsync(
            "sessions-other@example.com",
            "sessionsother",
            "Sessions Other",
            Guid.NewGuid(),
            AuthRoles.VIEWER);

        TokenResponse firstLogin = await LoginAsync("sessions-viewer@example.com", "SessionsTest/1.0");
        await LoginAsync("sessions-viewer@example.com", "SessionsTest/2.0");
        TokenResponse revokedLogin = await LoginAsync("sessions-viewer@example.com", "SessionsTest/Revoked");
        await LoginAsync("sessions-other@example.com", "SessionsTest/Other");
        await AddInactiveSessionAsync(currentUser.Id);

        HttpResponseMessage logoutResponse = await AppHttpClient.PostAsJsonAsync(
            "/api/auth/logout",
            new RefreshTokenRequest(revokedLogin.RefreshToken));
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using HttpRequestMessage request = new(HttpMethod.Get, "/api/auth/sessions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", firstLogin.AccessToken);

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        Envelope<IReadOnlyList<AuthSessionResponse>>? envelope =
            await response.Content.ReadFromJsonAsync<Envelope<IReadOnlyList<AuthSessionResponse>>>();

        envelope.Should().NotBeNull();
        envelope!.Result.Should().NotBeNull();

        IReadOnlyList<AuthSessionResponse> sessions = envelope.Result!;

        sessions.Should().HaveCount(2);
        sessions.Select(session => session.UserAgent)
            .Should()
            .BeEquivalentTo("SessionsTest/1.0", "SessionsTest/2.0");
        sessions.Should().OnlyContain(session => session.ExpiresAt > DateTime.UtcNow);
        sessions.Should().OnlyContain(session => session.Id != Guid.Empty);

        List<RefreshToken> allSavedTokens = await ExecuteInDb(dbContext => dbContext.RefreshTokens.ToListAsync());
        allSavedTokens.Where(token => token.UserId == currentUser.Id).Should().HaveCount(4);
    }

    [Fact]
    public async Task GetCurrentUserSessions_Without_Access_Token_Should_Return_Unauthorized()
    {
        HttpResponseMessage response = await AppHttpClient.GetAsync("/api/auth/sessions");

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

    private async Task AddInactiveSessionAsync(Guid userId)
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();

        AuthServiceDbContext dbContext = scope.ServiceProvider.GetRequiredService<AuthServiceDbContext>();
        RefreshToken inactiveToken = RefreshToken.Create(
            userId,
            new string('a', 64),
            DateTime.UtcNow.AddDays(1),
            "127.0.0.1",
            "SessionsTest/Inactive").Value;
        inactiveToken.Revoke("127.0.0.1");

        dbContext.RefreshTokens.Add(inactiveToken);
        await dbContext.SaveChangesAsync();
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
