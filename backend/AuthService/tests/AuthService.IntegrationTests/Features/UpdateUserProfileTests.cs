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

public sealed class UpdateUserProfileTests : AuthServiceBaseTests
{
    public UpdateUserProfileTests(AuthServiceTestWebFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task UpdateUserProfile_By_CompanyAdmin_Should_Update_Own_Company_User_DisplayName()
    {
        Guid companyId = Guid.NewGuid();
        await CreateIdentityUserAsync(
            "profile-company-admin@example.com",
            "profilecompanyadmin",
            "Profile Company Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);
        ApplicationUser targetUser = await CreateIdentityUserAsync(
            "profile-operator@example.com",
            "profileoperator",
            "Old Display",
            companyId,
            AuthRoles.OPERATOR);

        TokenResponse login = await LoginAsync("profile-company-admin@example.com");
        using HttpRequestMessage request = CreateAuthorizedPatchRequest(
            $"/api/users/{targetUser.Id}/profile",
            login.AccessToken,
            new UpdateUserProfileRequest("New Display"));

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        CompanyUserDetailsResponse changedUser = await ReadUserDetailsAsync(response);
        changedUser.UserId.Should().Be(targetUser.Id);
        changedUser.DisplayName.Should().Be("New Display");
        changedUser.Email.Should().Be("profile-operator@example.com");
        changedUser.Username.Should().Be("profileoperator");
        changedUser.Roles.Should().BeEquivalentTo(AuthRoles.OPERATOR);

        string? savedDisplayName = await ExecuteInDb(dbContext => dbContext.Users
            .Where(user => user.Id == targetUser.Id)
            .Select(user => user.DisplayName == null ? null : user.DisplayName.Value)
            .SingleAsync());
        savedDisplayName.Should().Be("New Display");
    }

    [Fact]
    public async Task UpdateUserProfile_With_Null_DisplayName_Should_Clear_DisplayName()
    {
        Guid companyId = Guid.NewGuid();
        await CreateIdentityUserAsync(
            "profile-clear-admin@example.com",
            "profileclearadmin",
            "Profile Clear Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);
        ApplicationUser targetUser = await CreateIdentityUserAsync(
            "profile-clear-user@example.com",
            "profileclearuser",
            "Display To Clear",
            companyId,
            AuthRoles.VIEWER);

        TokenResponse login = await LoginAsync("profile-clear-admin@example.com");
        using HttpRequestMessage request = CreateAuthorizedPatchRequest(
            $"/api/users/{targetUser.Id}/profile",
            login.AccessToken,
            new UpdateUserProfileRequest(null));

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        CompanyUserDetailsResponse changedUser = await ReadUserDetailsAsync(response);
        changedUser.DisplayName.Should().BeNull();
    }

    [Fact]
    public async Task UpdateUserProfile_By_CompanyAdmin_For_Another_Company_User_Should_Return_NotFound()
    {
        Guid companyId = Guid.NewGuid();
        Guid anotherCompanyId = Guid.NewGuid();
        await CreateIdentityUserAsync(
            "profile-boundary-admin@example.com",
            "profileboundaryadmin",
            "Profile Boundary Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);
        ApplicationUser targetUser = await CreateIdentityUserAsync(
            "profile-other-company@example.com",
            "profileothercompany",
            "Profile Other Company",
            anotherCompanyId,
            AuthRoles.VIEWER);

        TokenResponse login = await LoginAsync("profile-boundary-admin@example.com");
        using HttpRequestMessage request = CreateAuthorizedPatchRequest(
            $"/api/users/{targetUser.Id}/profile",
            login.AccessToken,
            new UpdateUserProfileRequest("Should Not Save"));

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        string? savedDisplayName = await ExecuteInDb(dbContext => dbContext.Users
            .Where(user => user.Id == targetUser.Id)
            .Select(user => user.DisplayName == null ? null : user.DisplayName.Value)
            .SingleAsync());
        savedDisplayName.Should().Be("Profile Other Company");
    }

    [Fact]
    public async Task UpdateUserProfile_By_SystemAdmin_Should_Update_Another_Company_User()
    {
        await CreateIdentityUserAsync(
            "profile-system-admin@example.com",
            "profilesystemadmin",
            "Profile System Admin",
            Guid.NewGuid(),
            AuthRoles.SYSTEM_ADMIN);
        ApplicationUser targetUser = await CreateIdentityUserAsync(
            "profile-system-target@example.com",
            "profilesystemtarget",
            "Profile System Target",
            Guid.NewGuid(),
            AuthRoles.TECHNICIAN);

        TokenResponse login = await LoginAsync("profile-system-admin@example.com");
        using HttpRequestMessage request = CreateAuthorizedPatchRequest(
            $"/api/users/{targetUser.Id}/profile",
            login.AccessToken,
            new UpdateUserProfileRequest("System Updated"));

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        CompanyUserDetailsResponse changedUser = await ReadUserDetailsAsync(response);
        changedUser.DisplayName.Should().Be("System Updated");
    }

    [Fact]
    public async Task UpdateUserProfile_Without_Access_Token_Should_Return_Unauthorized()
    {
        using HttpRequestMessage request = new(HttpMethod.Patch, $"/api/users/{Guid.NewGuid()}/profile")
        {
            Content = JsonContent.Create(new UpdateUserProfileRequest("No Auth"))
        };

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateUserProfile_Without_UsersManage_Permission_Should_Return_Forbidden()
    {
        Guid companyId = Guid.NewGuid();
        ApplicationUser viewer = await CreateIdentityUserAsync(
            "profile-viewer@example.com",
            "profileviewer",
            "Profile Viewer",
            companyId,
            AuthRoles.VIEWER);

        TokenResponse login = await LoginAsync("profile-viewer@example.com");
        using HttpRequestMessage request = CreateAuthorizedPatchRequest(
            $"/api/users/{viewer.Id}/profile",
            login.AccessToken,
            new UpdateUserProfileRequest("Forbidden"));

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateUserProfile_With_Unknown_User_Should_Return_NotFound()
    {
        Guid companyId = Guid.NewGuid();
        await CreateIdentityUserAsync(
            "profile-unknown-admin@example.com",
            "profileunknownadmin",
            "Profile Unknown Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);

        TokenResponse login = await LoginAsync("profile-unknown-admin@example.com");
        using HttpRequestMessage request = CreateAuthorizedPatchRequest(
            $"/api/users/{Guid.NewGuid()}/profile",
            login.AccessToken,
            new UpdateUserProfileRequest("Unknown"));

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static HttpRequestMessage CreateAuthorizedPatchRequest(
        string requestUri,
        string accessToken,
        UpdateUserProfileRequest profileRequest)
    {
        HttpRequestMessage request = new(HttpMethod.Patch, requestUri)
        {
            Content = JsonContent.Create(profileRequest)
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
