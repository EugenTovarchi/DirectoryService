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

public sealed class RevokeUserSessionsTests : AuthServiceBaseTests
{
    public RevokeUserSessionsTests(AuthServiceTestWebFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task RevokeUserSessions_By_CompanyAdmin_Should_Revoke_Own_Company_User_Sessions()
    {
        Guid companyId = Guid.NewGuid();
        await CreateIdentityUserAsync(
            "admin-revoke-company-admin@example.com",
            "adminrevokecompanyadmin",
            "Admin Revoke Company Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);
        ApplicationUser targetUser = await CreateIdentityUserAsync(
            "admin-revoke-operator@example.com",
            "adminrevokeoperator",
            "Admin Revoke Operator",
            companyId,
            AuthRoles.OPERATOR);

        TokenResponse targetLogin = await LoginAsync("admin-revoke-operator@example.com");
        TokenResponse adminLogin = await LoginAsync("admin-revoke-company-admin@example.com");
        using HttpRequestMessage request = CreateAuthorizedPostRequest(
            $"/api/users/{targetUser.Id}/revoke-sessions",
            adminLogin.AccessToken);

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        List<RefreshToken> targetTokens = await ExecuteInDb(dbContext => dbContext.RefreshTokens
            .Where(token => token.UserId == targetUser.Id)
            .ToListAsync());
        targetTokens.Should().OnlyContain(token => token.RevokedAt != null);

        HttpResponseMessage refreshResponse = await AppHttpClient.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequest(targetLogin.RefreshToken));
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RevokeUserSessions_By_CompanyAdmin_For_Another_Company_User_Should_Return_NotFound()
    {
        Guid companyId = Guid.NewGuid();
        Guid anotherCompanyId = Guid.NewGuid();
        await CreateIdentityUserAsync(
            "admin-revoke-boundary-admin@example.com",
            "adminrevokeboundaryadmin",
            "Admin Revoke Boundary Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);
        ApplicationUser targetUser = await CreateIdentityUserAsync(
            "admin-revoke-other-company@example.com",
            "adminrevokeothercompany",
            "Admin Revoke Other Company",
            anotherCompanyId,
            AuthRoles.VIEWER);

        await LoginAsync("admin-revoke-other-company@example.com");
        TokenResponse adminLogin = await LoginAsync("admin-revoke-boundary-admin@example.com");
        using HttpRequestMessage request = CreateAuthorizedPostRequest(
            $"/api/users/{targetUser.Id}/revoke-sessions",
            adminLogin.AccessToken);

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        List<RefreshToken> targetTokens = await ExecuteInDb(dbContext => dbContext.RefreshTokens
            .Where(token => token.UserId == targetUser.Id)
            .ToListAsync());
        targetTokens.Should().OnlyContain(token => token.RevokedAt == null);
    }

    [Fact]
    public async Task RevokeUserSessions_By_SystemAdmin_Should_Revoke_Another_Company_User_Sessions()
    {
        Guid systemCompanyId = Guid.NewGuid();
        Guid anotherCompanyId = Guid.NewGuid();
        await CreateIdentityUserAsync(
            "admin-revoke-system-admin@example.com",
            "adminrevokesystemadmin",
            "Admin Revoke System Admin",
            systemCompanyId,
            AuthRoles.SYSTEM_ADMIN);
        ApplicationUser targetUser = await CreateIdentityUserAsync(
            "admin-revoke-system-target@example.com",
            "adminrevokesystemtarget",
            "Admin Revoke System Target",
            anotherCompanyId,
            AuthRoles.TECHNICIAN);

        await LoginAsync("admin-revoke-system-target@example.com");
        TokenResponse adminLogin = await LoginAsync("admin-revoke-system-admin@example.com");
        using HttpRequestMessage request = CreateAuthorizedPostRequest(
            $"/api/users/{targetUser.Id}/revoke-sessions",
            adminLogin.AccessToken);

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        List<RefreshToken> targetTokens = await ExecuteInDb(dbContext => dbContext.RefreshTokens
            .Where(token => token.UserId == targetUser.Id)
            .ToListAsync());
        targetTokens.Should().OnlyContain(token => token.RevokedAt != null);
    }

    [Fact]
    public async Task RevokeUserSessions_Without_Access_Token_Should_Return_Unauthorized()
    {
        HttpResponseMessage response = await AppHttpClient.PostAsync(
            $"/api/users/{Guid.NewGuid()}/revoke-sessions",
            content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RevokeUserSessions_Without_UsersManage_Permission_Should_Return_Forbidden()
    {
        Guid companyId = Guid.NewGuid();
        ApplicationUser viewer = await CreateIdentityUserAsync(
            "admin-revoke-viewer@example.com",
            "adminrevokeviewer",
            "Admin Revoke Viewer",
            companyId,
            AuthRoles.VIEWER);

        TokenResponse login = await LoginAsync("admin-revoke-viewer@example.com");
        using HttpRequestMessage request = CreateAuthorizedPostRequest(
            $"/api/users/{viewer.Id}/revoke-sessions",
            login.AccessToken);

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RevokeUserSessions_With_Unknown_User_Should_Return_NotFound()
    {
        Guid companyId = Guid.NewGuid();
        await CreateIdentityUserAsync(
            "admin-revoke-unknown-admin@example.com",
            "adminrevokeunknownadmin",
            "Admin Revoke Unknown Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);

        TokenResponse login = await LoginAsync("admin-revoke-unknown-admin@example.com");
        using HttpRequestMessage request = CreateAuthorizedPostRequest(
            $"/api/users/{Guid.NewGuid()}/revoke-sessions",
            login.AccessToken);

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RevokeUserSessions_With_Self_User_Should_Return_BadRequest()
    {
        Guid companyId = Guid.NewGuid();
        ApplicationUser admin = await CreateIdentityUserAsync(
            "admin-revoke-self-admin@example.com",
            "adminrevokeselfadmin",
            "Admin Revoke Self Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);

        TokenResponse login = await LoginAsync("admin-revoke-self-admin@example.com");
        using HttpRequestMessage request = CreateAuthorizedPostRequest(
            $"/api/users/{admin.Id}/revoke-sessions",
            login.AccessToken);

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RevokeUserSessions_With_No_Active_Sessions_Should_Return_Ok()
    {
        Guid companyId = Guid.NewGuid();
        await CreateIdentityUserAsync(
            "admin-revoke-empty-admin@example.com",
            "adminrevokeemptyadmin",
            "Admin Revoke Empty Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);
        ApplicationUser targetUser = await CreateIdentityUserAsync(
            "admin-revoke-empty-target@example.com",
            "adminrevokeemptytarget",
            "Admin Revoke Empty Target",
            companyId,
            AuthRoles.VIEWER);

        TokenResponse login = await LoginAsync("admin-revoke-empty-admin@example.com");
        using HttpRequestMessage request = CreateAuthorizedPostRequest(
            $"/api/users/{targetUser.Id}/revoke-sessions",
            login.AccessToken);

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static HttpRequestMessage CreateAuthorizedPostRequest(string requestUri, string accessToken)
    {
        HttpRequestMessage request = new(HttpMethod.Post, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return request;
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
