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

public sealed class ChangeUserStatusTests : AuthServiceBaseTests
{
    public ChangeUserStatusTests(AuthServiceTestWebFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task ChangeUserStatus_By_CompanyAdmin_Should_Deactivate_Own_Company_User()
    {
        Guid companyId = Guid.NewGuid();
        await CreateIdentityUserAsync(
            "status-company-admin@example.com",
            "statuscompanyadmin",
            "Status Company Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);
        ApplicationUser targetUser = await CreateIdentityUserAsync(
            "status-operator@example.com",
            "statusoperator",
            "Status Operator",
            companyId,
            AuthRoles.OPERATOR);

        TokenResponse login = await LoginAsync("status-company-admin@example.com");
        using HttpRequestMessage request = CreateAuthorizedPatchRequest(
            $"/api/users/{targetUser.Id}/change-status",
            login.AccessToken,
            new ChangeUserStatusRequest(false));

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        CompanyUserDetailsResponse changedUser = await ReadUserDetailsAsync(response);
        changedUser.UserId.Should().Be(targetUser.Id);
        changedUser.IsActive.Should().BeFalse();
        changedUser.CompanyId.Should().Be(companyId);

        bool savedUserIsActive = await ExecuteInDb(dbContext => dbContext.Users
            .Where(user => user.Id == targetUser.Id)
            .Select(user => user.IsActive)
            .SingleAsync());
        savedUserIsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ChangeUserStatus_By_CompanyAdmin_For_Another_Company_User_Should_Return_NotFound()
    {
        Guid companyId = Guid.NewGuid();
        Guid anotherCompanyId = Guid.NewGuid();
        await CreateIdentityUserAsync(
            "status-boundary-admin@example.com",
            "statusboundaryadmin",
            "Status Boundary Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);
        ApplicationUser targetUser = await CreateIdentityUserAsync(
            "status-other-company@example.com",
            "statusothercompany",
            "Status Other Company",
            anotherCompanyId,
            AuthRoles.VIEWER);

        TokenResponse login = await LoginAsync("status-boundary-admin@example.com");
        using HttpRequestMessage request = CreateAuthorizedPatchRequest(
            $"/api/users/{targetUser.Id}/change-status",
            login.AccessToken,
            new ChangeUserStatusRequest(false));

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        bool savedUserIsActive = await ExecuteInDb(dbContext => dbContext.Users
            .Where(user => user.Id == targetUser.Id)
            .Select(user => user.IsActive)
            .SingleAsync());
        savedUserIsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ChangeUserStatus_By_SystemAdmin_Should_Deactivate_Another_Company_User()
    {
        Guid systemCompanyId = Guid.NewGuid();
        Guid anotherCompanyId = Guid.NewGuid();
        await CreateIdentityUserAsync(
            "status-system-admin@example.com",
            "statussystemadmin",
            "Status System Admin",
            systemCompanyId,
            AuthRoles.SYSTEM_ADMIN);
        ApplicationUser targetUser = await CreateIdentityUserAsync(
            "status-system-target@example.com",
            "statussystemtarget",
            "Status System Target",
            anotherCompanyId,
            AuthRoles.TECHNICIAN);

        TokenResponse login = await LoginAsync("status-system-admin@example.com");
        using HttpRequestMessage request = CreateAuthorizedPatchRequest(
            $"/api/users/{targetUser.Id}/change-status",
            login.AccessToken,
            new ChangeUserStatusRequest(false));

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        CompanyUserDetailsResponse changedUser = await ReadUserDetailsAsync(response);
        changedUser.UserId.Should().Be(targetUser.Id);
        changedUser.IsActive.Should().BeFalse();
        changedUser.CompanyId.Should().Be(anotherCompanyId);
    }

    [Fact]
    public async Task ChangeUserStatus_Should_Activate_Inactive_User()
    {
        Guid companyId = Guid.NewGuid();
        await CreateIdentityUserAsync(
            "status-activate-admin@example.com",
            "statusactivateadmin",
            "Status Activate Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);
        ApplicationUser targetUser = await CreateIdentityUserAsync(
            "status-inactive-user@example.com",
            "statusinactiveuser",
            "Status Inactive User",
            companyId,
            AuthRoles.VIEWER,
            isActive: false);

        TokenResponse login = await LoginAsync("status-activate-admin@example.com");
        using HttpRequestMessage request = CreateAuthorizedPatchRequest(
            $"/api/users/{targetUser.Id}/change-status",
            login.AccessToken,
            new ChangeUserStatusRequest(true));

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        CompanyUserDetailsResponse changedUser = await ReadUserDetailsAsync(response);
        changedUser.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ChangeUserStatus_Without_Access_Token_Should_Return_Unauthorized()
    {
        using HttpRequestMessage request = new(HttpMethod.Patch, $"/api/users/{Guid.NewGuid()}/change-status")
        {
            Content = JsonContent.Create(new ChangeUserStatusRequest(false))
        };

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangeUserStatus_Without_UsersManage_Permission_Should_Return_Forbidden()
    {
        Guid companyId = Guid.NewGuid();
        ApplicationUser viewer = await CreateIdentityUserAsync(
            "status-viewer@example.com",
            "statusviewer",
            "Status Viewer",
            companyId,
            AuthRoles.VIEWER);

        TokenResponse login = await LoginAsync("status-viewer@example.com");
        using HttpRequestMessage request = CreateAuthorizedPatchRequest(
            $"/api/users/{viewer.Id}/change-status",
            login.AccessToken,
            new ChangeUserStatusRequest(false));

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ChangeUserStatus_With_Unknown_User_Should_Return_NotFound()
    {
        Guid companyId = Guid.NewGuid();
        await CreateIdentityUserAsync(
            "status-unknown-admin@example.com",
            "statusunknownadmin",
            "Status Unknown Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);

        TokenResponse login = await LoginAsync("status-unknown-admin@example.com");
        using HttpRequestMessage request = CreateAuthorizedPatchRequest(
            $"/api/users/{Guid.NewGuid()}/change-status",
            login.AccessToken,
            new ChangeUserStatusRequest(false));

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ChangeUserStatus_With_Self_Deactivation_Should_Return_BadRequest()
    {
        Guid companyId = Guid.NewGuid();
        ApplicationUser admin = await CreateIdentityUserAsync(
            "status-self-admin@example.com",
            "statusselfadmin",
            "Status Self Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);

        TokenResponse login = await LoginAsync("status-self-admin@example.com");
        using HttpRequestMessage request = CreateAuthorizedPatchRequest(
            $"/api/users/{admin.Id}/change-status",
            login.AccessToken,
            new ChangeUserStatusRequest(false));

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        bool savedUserIsActive = await ExecuteInDb(dbContext => dbContext.Users
            .Where(user => user.Id == admin.Id)
            .Select(user => user.IsActive)
            .SingleAsync());
        savedUserIsActive.Should().BeTrue();
    }

    private static HttpRequestMessage CreateAuthorizedPatchRequest(
        string requestUri,
        string accessToken,
        ChangeUserStatusRequest statusRequest)
    {
        HttpRequestMessage request = new(HttpMethod.Patch, requestUri)
        {
            Content = JsonContent.Create(statusRequest)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return request;
    }

    private static async Task<CompanyUserDetailsResponse> ReadUserDetailsAsync(HttpResponseMessage response)
    {
        Envelope<CompanyUserDetailsResponse>? envelope =
            await response.Content.ReadFromJsonAsync<Envelope<CompanyUserDetailsResponse>>();

        envelope.Should().NotBeNull();
        envelope!.Result.Should().NotBeNull();

        return envelope.Result!;
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
        string role,
        bool isActive = true)
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

        if (!isActive)
            user.Deactivate();

        IdentityResult createResult = await userManager.CreateAsync(user, "password123");
        createResult.Succeeded.Should().BeTrue(string.Join("; ", createResult.Errors.Select(error => error.Description)));

        IdentityResult roleResult = await userManager.AddToRoleAsync(user, role);
        roleResult.Succeeded.Should().BeTrue(string.Join("; ", roleResult.Errors.Select(error => error.Description)));

        return user;
    }
}
