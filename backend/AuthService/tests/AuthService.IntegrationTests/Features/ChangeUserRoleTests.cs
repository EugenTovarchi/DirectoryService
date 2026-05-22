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

public sealed class ChangeUserRoleTests : AuthServiceBaseTests
{
    public ChangeUserRoleTests(AuthServiceTestWebFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task ChangeUserRole_By_CompanyAdmin_Should_Change_Own_Company_User_Role()
    {
        Guid companyId = Guid.NewGuid();
        await CreateIdentityUserAsync(
            "role-company-admin@example.com",
            "rolecompanyadmin",
            "Role Company Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);
        ApplicationUser targetUser = await CreateIdentityUserAsync(
            "role-viewer@example.com",
            "roleviewer",
            "Role Viewer",
            companyId,
            AuthRoles.VIEWER);

        TokenResponse login = await LoginAsync("role-company-admin@example.com");
        using HttpRequestMessage request = CreateAuthorizedPatchRequest(
            $"/api/users/{targetUser.Id}/change-role",
            login.AccessToken,
            new ChangeUserRoleRequest(AuthRoles.OPERATOR));

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        CompanyUserDetailsResponse changedUser = await ReadUserDetailsAsync(response);
        changedUser.UserId.Should().Be(targetUser.Id);
        changedUser.Roles.Should().BeEquivalentTo(AuthRoles.OPERATOR);

        IReadOnlyCollection<string> savedRoles = await GetUserRolesAsync(targetUser.Id);
        savedRoles.Should().BeEquivalentTo(AuthRoles.OPERATOR);
    }

    [Fact]
    public async Task ChangeUserRole_By_CompanyAdmin_For_Another_Company_User_Should_Return_NotFound()
    {
        Guid companyId = Guid.NewGuid();
        Guid anotherCompanyId = Guid.NewGuid();
        await CreateIdentityUserAsync(
            "role-boundary-admin@example.com",
            "roleboundaryadmin",
            "Role Boundary Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);
        ApplicationUser targetUser = await CreateIdentityUserAsync(
            "role-other-company@example.com",
            "roleothercompany",
            "Role Other Company",
            anotherCompanyId,
            AuthRoles.VIEWER);

        TokenResponse login = await LoginAsync("role-boundary-admin@example.com");
        using HttpRequestMessage request = CreateAuthorizedPatchRequest(
            $"/api/users/{targetUser.Id}/change-role",
            login.AccessToken,
            new ChangeUserRoleRequest(AuthRoles.OPERATOR));

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        IReadOnlyCollection<string> savedRoles = await GetUserRolesAsync(targetUser.Id);
        savedRoles.Should().BeEquivalentTo(AuthRoles.VIEWER);
    }

    [Fact]
    public async Task ChangeUserRole_By_SystemAdmin_Should_Change_Another_Company_User_Role()
    {
        Guid systemCompanyId = Guid.NewGuid();
        Guid anotherCompanyId = Guid.NewGuid();
        await CreateIdentityUserAsync(
            "role-system-admin@example.com",
            "rolesystemadmin",
            "Role System Admin",
            systemCompanyId,
            AuthRoles.SYSTEM_ADMIN);
        ApplicationUser targetUser = await CreateIdentityUserAsync(
            "role-system-target@example.com",
            "rolesystemtarget",
            "Role System Target",
            anotherCompanyId,
            AuthRoles.VIEWER);

        TokenResponse login = await LoginAsync("role-system-admin@example.com");
        using HttpRequestMessage request = CreateAuthorizedPatchRequest(
            $"/api/users/{targetUser.Id}/change-role",
            login.AccessToken,
            new ChangeUserRoleRequest(AuthRoles.TECHNICIAN));

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        CompanyUserDetailsResponse changedUser = await ReadUserDetailsAsync(response);
        changedUser.Roles.Should().BeEquivalentTo(AuthRoles.TECHNICIAN);
    }

    [Fact]
    public async Task ChangeUserRole_Without_Access_Token_Should_Return_Unauthorized()
    {
        using HttpRequestMessage request = new(HttpMethod.Patch, $"/api/users/{Guid.NewGuid()}/change-role")
        {
            Content = JsonContent.Create(new ChangeUserRoleRequest(AuthRoles.OPERATOR))
        };

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangeUserRole_Without_UsersManage_Permission_Should_Return_Forbidden()
    {
        Guid companyId = Guid.NewGuid();
        ApplicationUser viewer = await CreateIdentityUserAsync(
            "role-permission-viewer@example.com",
            "rolepermissionviewer",
            "Role Permission Viewer",
            companyId,
            AuthRoles.VIEWER);

        TokenResponse login = await LoginAsync("role-permission-viewer@example.com");
        using HttpRequestMessage request = CreateAuthorizedPatchRequest(
            $"/api/users/{viewer.Id}/change-role",
            login.AccessToken,
            new ChangeUserRoleRequest(AuthRoles.OPERATOR));

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ChangeUserRole_With_Unknown_User_Should_Return_NotFound()
    {
        Guid companyId = Guid.NewGuid();
        await CreateIdentityUserAsync(
            "role-unknown-admin@example.com",
            "roleunknownadmin",
            "Role Unknown Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);

        TokenResponse login = await LoginAsync("role-unknown-admin@example.com");
        using HttpRequestMessage request = CreateAuthorizedPatchRequest(
            $"/api/users/{Guid.NewGuid()}/change-role",
            login.AccessToken,
            new ChangeUserRoleRequest(AuthRoles.OPERATOR));

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ChangeUserRole_With_Invalid_Role_Should_Return_BadRequest()
    {
        Guid companyId = Guid.NewGuid();
        await CreateIdentityUserAsync(
            "role-invalid-admin@example.com",
            "roleinvalidadmin",
            "Role Invalid Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);
        ApplicationUser targetUser = await CreateIdentityUserAsync(
            "role-invalid-target@example.com",
            "roleinvalidtarget",
            "Role Invalid Target",
            companyId,
            AuthRoles.VIEWER);

        TokenResponse login = await LoginAsync("role-invalid-admin@example.com");
        using HttpRequestMessage request = CreateAuthorizedPatchRequest(
            $"/api/users/{targetUser.Id}/change-role",
            login.AccessToken,
            new ChangeUserRoleRequest("UnknownRole"));

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangeUserRole_With_Self_Role_Change_Should_Return_BadRequest()
    {
        Guid companyId = Guid.NewGuid();
        ApplicationUser admin = await CreateIdentityUserAsync(
            "role-self-admin@example.com",
            "roleselfadmin",
            "Role Self Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);

        TokenResponse login = await LoginAsync("role-self-admin@example.com");
        using HttpRequestMessage request = CreateAuthorizedPatchRequest(
            $"/api/users/{admin.Id}/change-role",
            login.AccessToken,
            new ChangeUserRoleRequest(AuthRoles.VIEWER));

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        IReadOnlyCollection<string> savedRoles = await GetUserRolesAsync(admin.Id);
        savedRoles.Should().BeEquivalentTo(AuthRoles.COMPANY_ADMIN);
    }

    [Fact]
    public async Task ChangeUserRole_By_CompanyAdmin_To_SystemAdmin_Should_Return_BadRequest()
    {
        Guid companyId = Guid.NewGuid();
        await CreateIdentityUserAsync(
            "role-system-assign-admin@example.com",
            "rolesystemassignadmin",
            "Role System Assign Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);
        ApplicationUser targetUser = await CreateIdentityUserAsync(
            "role-system-assign-target@example.com",
            "rolesystemassigntarget",
            "Role System Assign Target",
            companyId,
            AuthRoles.VIEWER);

        TokenResponse login = await LoginAsync("role-system-assign-admin@example.com");
        using HttpRequestMessage request = CreateAuthorizedPatchRequest(
            $"/api/users/{targetUser.Id}/change-role",
            login.AccessToken,
            new ChangeUserRoleRequest(AuthRoles.SYSTEM_ADMIN));

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        IReadOnlyCollection<string> savedRoles = await GetUserRolesAsync(targetUser.Id);
        savedRoles.Should().BeEquivalentTo(AuthRoles.VIEWER);
    }

    private static HttpRequestMessage CreateAuthorizedPatchRequest(
        string requestUri,
        string accessToken,
        ChangeUserRoleRequest roleRequest)
    {
        HttpRequestMessage request = new(HttpMethod.Patch, requestUri)
        {
            Content = JsonContent.Create(roleRequest)
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

    private async Task<IReadOnlyCollection<string>> GetUserRolesAsync(Guid userId)
    {
        return await ExecuteInDb(dbContext => dbContext.UserRoles
            .Where(userRole => userRole.UserId == userId)
            .Join(
                dbContext.Roles,
                userRole => userRole.RoleId,
                role => role.Id,
                (_, role) => role.Name!)
            .ToListAsync());
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
