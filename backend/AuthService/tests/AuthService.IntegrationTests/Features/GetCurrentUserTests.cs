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

public sealed class GetCurrentUserTests : AuthServiceBaseTests
{
    public GetCurrentUserTests(AuthServiceTestWebFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetCurrentUser_With_Authenticated_User_Should_Return_User_Roles_Permissions_And_Company_Context()
    {
        Guid companyId = Guid.NewGuid();
        ApplicationUser user = await CreateIdentityUserAsync(
            "me-company-admin@example.com",
            "mecompanyadmin",
            "Me Company Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);

        TokenResponse login = await LoginAsync("me-company-admin@example.com");

        using HttpRequestMessage request = new(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        Envelope<CurrentUserResponse>? envelope =
            await response.Content.ReadFromJsonAsync<Envelope<CurrentUserResponse>>();

        envelope.Should().NotBeNull();
        envelope!.Result.Should().NotBeNull();

        CurrentUserResponse currentUser = envelope.Result!;

        currentUser.Id.Should().Be(user.Id);
        currentUser.Email.Should().Be("me-company-admin@example.com");
        currentUser.Username.Should().Be("mecompanyadmin");
        currentUser.DisplayName.Should().Be("Me Company Admin");
        currentUser.CurrentCompanyId.Should().Be(companyId);
        currentUser.Roles.Should().BeEquivalentTo(AuthRoles.COMPANY_ADMIN);
        currentUser.Permissions.Should().BeEquivalentTo(
            AuthPermissions.USERS_MANAGE,
            AuthPermissions.DIRECTORY_READ,
            AuthPermissions.DIRECTORY_MANAGE,
            AuthPermissions.FILES_READ,
            AuthPermissions.FILES_UPLOAD,
            AuthPermissions.VIDEOS_READ,
            AuthPermissions.VIDEOS_UPLOAD);
    }

    [Fact]
    public async Task GetCurrentUser_Without_Access_Token_Should_Return_Unauthorized()
    {
        HttpResponseMessage response = await AppHttpClient.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
