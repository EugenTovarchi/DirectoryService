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
using SharedService.Core.Abstractions;
using SharedService.SharedKernel;

namespace AuthService.IntegrationTests.Features;

public sealed class GetUsersTests : AuthServiceBaseTests
{
    public GetUsersTests(AuthServiceTestWebFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetUsers_By_CompanyAdmin_Should_Return_Only_Current_Company_Users()
    {
        Guid companyId = Guid.NewGuid();
        Guid anotherCompanyId = Guid.NewGuid();

        await CreateIdentityUserAsync(
            "list-company-admin@example.com",
            "listcompanyadmin",
            "List Company Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);
        await CreateIdentityUserAsync(
            "list-operator@example.com",
            "listoperator",
            "List Operator",
            companyId,
            AuthRoles.OPERATOR);
        await CreateIdentityUserAsync(
            "list-technician@example.com",
            "listtechnician",
            "List Technician",
            companyId,
            AuthRoles.TECHNICIAN);
        await CreateIdentityUserAsync(
            "list-other-company@example.com",
            "listothercompany",
            "List Other Company",
            anotherCompanyId,
            AuthRoles.VIEWER);

        TokenResponse login = await LoginAsync("list-company-admin@example.com");
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/users?page=1&pageSize=20");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        PagedList<CompanyUserResponse> usersPage = await ReadUsersAsync(response);
        IReadOnlyCollection<CompanyUserResponse> users = usersPage.Items;
        usersPage.Page.Should().Be(1);
        usersPage.PageSize.Should().Be(20);
        usersPage.TotalCount.Should().Be(3);
        users.Select(user => user.Email).Should().BeEquivalentTo(
            "list-company-admin@example.com",
            "list-operator@example.com",
            "list-technician@example.com");
        users.Should().OnlyContain(user => user.CompanyId == companyId);
        users.Single(user => string.Equals(user.Email, "list-operator@example.com", StringComparison.Ordinal))
            .Roles.Should().BeEquivalentTo(AuthRoles.OPERATOR);
    }

    [Fact]
    public async Task GetUsers_By_SystemAdmin_Should_Return_All_Company_Users()
    {
        Guid systemCompanyId = Guid.NewGuid();
        Guid firstCompanyId = Guid.NewGuid();
        Guid secondCompanyId = Guid.NewGuid();

        await CreateIdentityUserAsync(
            "list-system-admin@example.com",
            "listsystemadmin",
            "List System Admin",
            systemCompanyId,
            AuthRoles.SYSTEM_ADMIN);
        await CreateIdentityUserAsync(
            "list-first-company@example.com",
            "listfirstcompany",
            "List First Company",
            firstCompanyId,
            AuthRoles.COMPANY_ADMIN);
        await CreateIdentityUserAsync(
            "list-second-company@example.com",
            "listsecondcompany",
            "List Second Company",
            secondCompanyId,
            AuthRoles.VIEWER);

        TokenResponse login = await LoginAsync("list-system-admin@example.com");
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/users?page=1&pageSize=20");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        PagedList<CompanyUserResponse> usersPage = await ReadUsersAsync(response);
        IReadOnlyCollection<CompanyUserResponse> users = usersPage.Items;
        usersPage.TotalCount.Should().Be(3);
        users.Select(user => user.Email).Should().BeEquivalentTo(
            "list-system-admin@example.com",
            "list-first-company@example.com",
            "list-second-company@example.com");
        users.Select(user => user.CompanyId).Should().Contain(systemCompanyId);
        users.Select(user => user.CompanyId).Should().Contain(firstCompanyId);
        users.Select(user => user.CompanyId).Should().Contain(secondCompanyId);
    }

    [Fact]
    public async Task GetUsers_Without_Access_Token_Should_Return_Unauthorized()
    {
        HttpResponseMessage response = await AppHttpClient.GetAsync("/api/users?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUsers_Without_UsersManage_Permission_Should_Return_Forbidden()
    {
        Guid companyId = Guid.NewGuid();
        await CreateIdentityUserAsync(
            "list-viewer@example.com",
            "listviewer",
            "List Viewer",
            companyId,
            AuthRoles.VIEWER);

        TokenResponse login = await LoginAsync("list-viewer@example.com");
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/users?page=1&pageSize=20");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetUsers_With_Second_Page_Should_Return_Paged_Company_Users()
    {
        Guid companyId = Guid.NewGuid();
        await CreateIdentityUserAsync(
            "list-page-admin@example.com",
            "listpageadmin",
            "List Page Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);
        await CreateIdentityUserAsync(
            "list-page-operator@example.com",
            "listpageoperator",
            "List Page Operator",
            companyId,
            AuthRoles.OPERATOR);
        await CreateIdentityUserAsync(
            "list-page-viewer@example.com",
            "listpageviewer",
            "List Page Viewer",
            companyId,
            AuthRoles.VIEWER);

        TokenResponse login = await LoginAsync("list-page-admin@example.com");
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/users?page=2&pageSize=2");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        PagedList<CompanyUserResponse> usersPage = await ReadUsersAsync(response);
        usersPage.Page.Should().Be(2);
        usersPage.PageSize.Should().Be(2);
        usersPage.TotalCount.Should().Be(3);
        usersPage.Items.Should().ContainSingle();
        usersPage.HasPreviousPage.Should().BeTrue();
        usersPage.HasNextpage.Should().BeFalse();
    }

    private static async Task<PagedList<CompanyUserResponse>> ReadUsersAsync(HttpResponseMessage response)
    {
        Envelope<PagedList<CompanyUserResponse>>? envelope =
            await response.Content.ReadFromJsonAsync<Envelope<PagedList<CompanyUserResponse>>>();

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
