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

public sealed class GetUserDetailsTests : AuthServiceBaseTests
{
    public GetUserDetailsTests(AuthServiceTestWebFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetUserDetails_By_CompanyAdmin_Should_Return_Own_Company_User()
    {
        Guid companyId = Guid.NewGuid();
        await CreateIdentityUserAsync(
            "details-company-admin@example.com",
            "detailscompanyadmin",
            "Details Company Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);
        ApplicationUser targetUser = await CreateIdentityUserAsync(
            "details-operator@example.com",
            "detailsoperator",
            "Details Operator",
            companyId,
            AuthRoles.OPERATOR);

        TokenResponse login = await LoginAsync("details-company-admin@example.com");
        using HttpRequestMessage request = CreateAuthorizedGetRequest(
            $"/api/users/{targetUser.Id}",
            login.AccessToken);

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        CompanyUserDetailsResponse user = await ReadUserDetailsAsync(response);
        user.UserId.Should().Be(targetUser.Id);
        user.Email.Should().Be("details-operator@example.com");
        user.Username.Should().Be("detailsoperator");
        user.DisplayName.Should().Be("Details Operator");
        user.CompanyId.Should().Be(companyId);
        user.IsActive.Should().BeTrue();
        user.Roles.Should().BeEquivalentTo(AuthRoles.OPERATOR);
        user.CreatedAt.Should().NotBe(default);
        user.UpdatedAt.Should().NotBe(default);
    }

    [Fact]
    public async Task GetUserDetails_By_CompanyAdmin_For_Another_Company_User_Should_Return_NotFound()
    {
        Guid companyId = Guid.NewGuid();
        Guid anotherCompanyId = Guid.NewGuid();
        await CreateIdentityUserAsync(
            "details-boundary-admin@example.com",
            "detailsboundaryadmin",
            "Details Boundary Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);
        ApplicationUser targetUser = await CreateIdentityUserAsync(
            "details-other-company@example.com",
            "detailsothercompany",
            "Details Other Company",
            anotherCompanyId,
            AuthRoles.VIEWER);

        TokenResponse login = await LoginAsync("details-boundary-admin@example.com");
        using HttpRequestMessage request = CreateAuthorizedGetRequest(
            $"/api/users/{targetUser.Id}",
            login.AccessToken);

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetUserDetails_By_SystemAdmin_Should_Return_Another_Company_User()
    {
        Guid systemCompanyId = Guid.NewGuid();
        Guid anotherCompanyId = Guid.NewGuid();
        await CreateIdentityUserAsync(
            "details-system-admin@example.com",
            "detailssystemadmin",
            "Details System Admin",
            systemCompanyId,
            AuthRoles.SYSTEM_ADMIN);
        ApplicationUser targetUser = await CreateIdentityUserAsync(
            "details-system-target@example.com",
            "detailssystemtarget",
            "Details System Target",
            anotherCompanyId,
            AuthRoles.TECHNICIAN);

        TokenResponse login = await LoginAsync("details-system-admin@example.com");
        using HttpRequestMessage request = CreateAuthorizedGetRequest(
            $"/api/users/{targetUser.Id}",
            login.AccessToken);

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        CompanyUserDetailsResponse user = await ReadUserDetailsAsync(response);
        user.UserId.Should().Be(targetUser.Id);
        user.CompanyId.Should().Be(anotherCompanyId);
        user.Roles.Should().BeEquivalentTo(AuthRoles.TECHNICIAN);
    }

    [Fact]
    public async Task GetUserDetails_Without_Access_Token_Should_Return_Unauthorized()
    {
        HttpResponseMessage response = await AppHttpClient.GetAsync($"/api/users/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUserDetails_Without_UsersManage_Permission_Should_Return_Forbidden()
    {
        Guid companyId = Guid.NewGuid();
        ApplicationUser viewer = await CreateIdentityUserAsync(
            "details-viewer@example.com",
            "detailsviewer",
            "Details Viewer",
            companyId,
            AuthRoles.VIEWER);

        TokenResponse login = await LoginAsync("details-viewer@example.com");
        using HttpRequestMessage request = CreateAuthorizedGetRequest(
            $"/api/users/{viewer.Id}",
            login.AccessToken);

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetUserDetails_With_Unknown_User_Should_Return_NotFound()
    {
        Guid companyId = Guid.NewGuid();
        await CreateIdentityUserAsync(
            "details-unknown-admin@example.com",
            "detailsunknownadmin",
            "Details Unknown Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);

        TokenResponse login = await LoginAsync("details-unknown-admin@example.com");
        using HttpRequestMessage request = CreateAuthorizedGetRequest(
            $"/api/users/{Guid.NewGuid()}",
            login.AccessToken);

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static HttpRequestMessage CreateAuthorizedGetRequest(string requestUri, string accessToken)
    {
        HttpRequestMessage request = new(HttpMethod.Get, requestUri);
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
