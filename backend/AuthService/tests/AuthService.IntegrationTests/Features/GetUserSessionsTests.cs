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

public sealed class GetUserSessionsTests : AuthServiceBaseTests
{
    public GetUserSessionsTests(AuthServiceTestWebFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetUserSessions_By_CompanyAdmin_Should_Return_Own_Company_User_Active_Sessions()
    {
        // Arrange
        Guid companyId = Guid.NewGuid();
        await CreateIdentityUserAsync(
            "admin-sessions-company-admin@example.com",
            "adminsessionscompanyadmin",
            "Admin Sessions Company Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);
        ApplicationUser targetUser = await CreateIdentityUserAsync(
            "admin-sessions-operator@example.com",
            "adminsessionsoperator",
            "Admin Sessions Operator",
            companyId,
            AuthRoles.OPERATOR);

        OpenIddictTestSession activeSession =
            await OpenIddictSessionTestHelper.CreateSessionAsync(Services, targetUser.Id);
        await OpenIddictSessionTestHelper.CreateSessionAsync(
            Services,
            targetUser.Id,
            revoked: true);
        await OpenIddictSessionTestHelper.CreateSessionAsync(
            Services,
            targetUser.Id,
            expirationDate: DateTimeOffset.UtcNow.AddMinutes(-1));

        OidcTestToken adminLogin = await LoginAsync("admin-sessions-company-admin@example.com", "AdminSessions/Admin");
        using HttpRequestMessage request = CreateAuthorizedGetRequest(
            $"/api/users/{targetUser.Id}/sessions",
            adminLogin.AccessToken);

        // Act
        HttpResponseMessage response = await AppHttpClient.SendAsync(request);
        IReadOnlyList<AuthSessionResponse> sessions = await ReadSessionsAsync(response);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        sessions.Should().ContainSingle();
        sessions.Single().Id.Should().Be(activeSession.AuthorizationId);
        sessions.Single().UserAgent.Should().BeNull();
        sessions.Single().CreatedByIp.Should().BeNull();
        sessions.Should().OnlyContain(session => session.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task GetUserSessions_By_CompanyAdmin_For_Another_Company_User_Should_Return_NotFound()
    {
        // Arrange
        Guid companyId = Guid.NewGuid();
        Guid anotherCompanyId = Guid.NewGuid();
        await CreateIdentityUserAsync(
            "admin-sessions-boundary-admin@example.com",
            "adminsessionsboundaryadmin",
            "Admin Sessions Boundary Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);
        ApplicationUser targetUser = await CreateIdentityUserAsync(
            "admin-sessions-other-company@example.com",
            "adminsessionsothercompany",
            "Admin Sessions Other Company",
            anotherCompanyId,
            AuthRoles.VIEWER);

        await LoginAsync("admin-sessions-other-company@example.com", "AdminSessions/Other");
        OidcTestToken adminLogin = await LoginAsync("admin-sessions-boundary-admin@example.com", "AdminSessions/Admin");
        using HttpRequestMessage request = CreateAuthorizedGetRequest(
            $"/api/users/{targetUser.Id}/sessions",
            adminLogin.AccessToken);

        // Act
        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetUserSessions_By_SystemAdmin_Should_Return_Another_Company_User_Sessions()
    {
        // Arrange
        Guid systemCompanyId = Guid.NewGuid();
        Guid anotherCompanyId = Guid.NewGuid();
        await CreateIdentityUserAsync(
            "admin-sessions-system-admin@example.com",
            "adminsessionssystemadmin",
            "Admin Sessions System Admin",
            systemCompanyId,
            AuthRoles.SYSTEM_ADMIN);
        ApplicationUser targetUser = await CreateIdentityUserAsync(
            "admin-sessions-system-target@example.com",
            "adminsessionssystemtarget",
            "Admin Sessions System Target",
            anotherCompanyId,
            AuthRoles.TECHNICIAN);

        OpenIddictTestSession targetSession =
            await OpenIddictSessionTestHelper.CreateSessionAsync(Services, targetUser.Id);
        OidcTestToken adminLogin = await LoginAsync("admin-sessions-system-admin@example.com", "AdminSessions/Admin");
        using HttpRequestMessage request = CreateAuthorizedGetRequest(
            $"/api/users/{targetUser.Id}/sessions",
            adminLogin.AccessToken);

        // Act
        HttpResponseMessage response = await AppHttpClient.SendAsync(request);
        IReadOnlyList<AuthSessionResponse> sessions = await ReadSessionsAsync(response);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        sessions.Should().ContainSingle();
        sessions.Single().Id.Should().Be(targetSession.AuthorizationId);
        sessions.Single().UserAgent.Should().BeNull();
        sessions.Single().CreatedByIp.Should().BeNull();
    }

    [Fact]
    public async Task GetUserSessions_Without_Access_Token_Should_Return_Unauthorized()
    {
        // Arrange

        // Act
        HttpResponseMessage response = await AppHttpClient.GetAsync($"/api/users/{Guid.NewGuid()}/sessions");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUserSessions_Without_UsersManage_Permission_Should_Return_Forbidden()
    {
        // Arrange
        Guid companyId = Guid.NewGuid();
        ApplicationUser viewer = await CreateIdentityUserAsync(
            "admin-sessions-viewer@example.com",
            "adminsessionsviewer",
            "Admin Sessions Viewer",
            companyId,
            AuthRoles.VIEWER);

        OidcTestToken login = await LoginAsync("admin-sessions-viewer@example.com", "AdminSessions/Viewer");
        using HttpRequestMessage request = CreateAuthorizedGetRequest(
            $"/api/users/{viewer.Id}/sessions",
            login.AccessToken);

        // Act
        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetUserSessions_With_Unknown_User_Should_Return_NotFound()
    {
        // Arrange
        Guid companyId = Guid.NewGuid();
        await CreateIdentityUserAsync(
            "admin-sessions-unknown-admin@example.com",
            "adminsessionsunknownadmin",
            "Admin Sessions Unknown Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);

        OidcTestToken login = await LoginAsync("admin-sessions-unknown-admin@example.com", "AdminSessions/Admin");
        using HttpRequestMessage request = CreateAuthorizedGetRequest(
            $"/api/users/{Guid.NewGuid()}/sessions",
            login.AccessToken);

        // Act
        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetUserSessions_With_Self_User_Should_Return_BadRequest()
    {
        // Arrange
        Guid companyId = Guid.NewGuid();
        ApplicationUser admin = await CreateIdentityUserAsync(
            "admin-sessions-self-admin@example.com",
            "adminsessionsselfadmin",
            "Admin Sessions Self Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);

        OidcTestToken login = await LoginAsync("admin-sessions-self-admin@example.com", "AdminSessions/Admin");
        using HttpRequestMessage request = CreateAuthorizedGetRequest(
            $"/api/users/{admin.Id}/sessions",
            login.AccessToken);

        // Act
        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetUserSessions_With_No_Active_Sessions_Should_Return_Empty_List()
    {
        // Arrange
        Guid companyId = Guid.NewGuid();
        await CreateIdentityUserAsync(
            "admin-sessions-empty-admin@example.com",
            "adminsessionsemptyadmin",
            "Admin Sessions Empty Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);
        ApplicationUser targetUser = await CreateIdentityUserAsync(
            "admin-sessions-empty-target@example.com",
            "adminsessionsemptytarget",
            "Admin Sessions Empty Target",
            companyId,
            AuthRoles.VIEWER);

        OidcTestToken login = await LoginAsync("admin-sessions-empty-admin@example.com", "AdminSessions/Admin");
        using HttpRequestMessage request = CreateAuthorizedGetRequest(
            $"/api/users/{targetUser.Id}/sessions",
            login.AccessToken);

        // Act
        HttpResponseMessage response = await AppHttpClient.SendAsync(request);
        IReadOnlyList<AuthSessionResponse> sessions = await ReadSessionsAsync(response);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        sessions.Should().BeEmpty();
    }

    private static HttpRequestMessage CreateAuthorizedGetRequest(string requestUri, string accessToken)
    {
        HttpRequestMessage request = new(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return request;
    }

    private static async Task<IReadOnlyList<AuthSessionResponse>> ReadSessionsAsync(HttpResponseMessage response)
    {
        Envelope<IReadOnlyList<AuthSessionResponse>>? envelope =
            await response.Content.ReadFromJsonAsync<Envelope<IReadOnlyList<AuthSessionResponse>>>();

        envelope.Should().NotBeNull();
        envelope!.Result.Should().NotBeNull();

        return envelope.Result!;
    }

    private Task<OidcTestToken> LoginAsync(string email, string userAgent)
    {
        return LoginWithOidcAsync(email, userAgent: userAgent);
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
