using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AuthService.Contracts.Requests;
using AuthService.Contracts.Responses;
using AuthService.Core.Abstractions;
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

public sealed class InviteUserTests : AuthServiceBaseTests
{
    public InviteUserTests(AuthServiceTestWebFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task InviteUser_With_UsersManage_Permission_Should_Create_Pending_Identity_User_And_Invite_Token()
    {
        Guid companyId = Guid.NewGuid();
        await CreateIdentityUserAsync(
            "invite-admin@example.com",
            "inviteadmin",
            "Invite Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);
        TokenResponse login = await LoginAsync("invite-admin@example.com");

        InviteUserRequest inviteRequest = new(
            "new-user@example.com",
            "newuser",
            "New User",
            companyId,
            AuthRoles.VIEWER);

        using HttpRequestMessage request = new(HttpMethod.Post, "/api/users/invite")
        {
            Content = JsonContent.Create(inviteRequest)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        Envelope<InviteUserResponse>? envelope =
            await response.Content.ReadFromJsonAsync<Envelope<InviteUserResponse>>();

        envelope.Should().NotBeNull();
        envelope!.Result.Should().NotBeNull();

        InviteUserResponse invitedUser = envelope.Result!;
        invitedUser.Email.Should().Be("new-user@example.com");
        invitedUser.Username.Should().Be("newuser");
        invitedUser.DisplayName.Should().Be("New User");
        invitedUser.CompanyId.Should().Be(companyId);
        invitedUser.Roles.Should().BeEquivalentTo(AuthRoles.VIEWER);
        invitedUser.InviteToken.Should().NotBeNullOrWhiteSpace();
        invitedUser.InviteTokenExpiresAt.Should().BeAfter(DateTime.UtcNow);
        invitedUser.InviteTokenExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(3), TimeSpan.FromMinutes(1));

        ApplicationUser savedUser = await ExecuteInDb(dbContext => dbContext.Users
            .SingleAsync(user => user.Id == invitedUser.UserId));

        savedUser.CurrentCompanyId.Should().Be(companyId);
        savedUser.IsActive.Should().BeFalse();
        savedUser.PasswordHash.Should().BeNull();

        int inviteTokenCount = await ExecuteInDb(dbContext => dbContext.UserInviteTokens
            .CountAsync(token => token.UserId == invitedUser.UserId));
        inviteTokenCount.Should().Be(1);

        await AssertUserCannotLoginAsync("new-user@example.com", "password123");

        HttpResponseMessage acceptResponse = await AppHttpClient.PostAsJsonAsync(
            "/api/auth/accept-invite",
            new AcceptInviteRequest(invitedUser.InviteToken, "password123"));
        acceptResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        UserInviteToken savedInviteToken = await ExecuteInDb(dbContext => dbContext.UserInviteTokens
            .SingleAsync(token => token.UserId == invitedUser.UserId));
        savedInviteToken.AcceptedAt.Should().NotBeNull();

        await AssertUserCanLoginAsync("new-user@example.com", "password123");
    }

    [Fact]
    public async Task AcceptInvite_With_Accepted_Token_Should_Return_BadRequest()
    {
        Guid companyId = Guid.NewGuid();
        await CreateIdentityUserAsync(
            "invite-reuse-admin@example.com",
            "invitereuseadmin",
            "Invite Reuse Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);
        TokenResponse login = await LoginAsync("invite-reuse-admin@example.com");

        InviteUserResponse invitedUser = await InviteUserAsync(
            login.AccessToken,
            new InviteUserRequest(
                "invite-reuse-user@example.com",
                "invitereuseuser",
                "Invite Reuse User",
                companyId,
                AuthRoles.VIEWER));

        HttpResponseMessage firstResponse = await AppHttpClient.PostAsJsonAsync(
            "/api/auth/accept-invite",
            new AcceptInviteRequest(invitedUser.InviteToken, "password123"));
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage secondResponse = await AppHttpClient.PostAsJsonAsync(
            "/api/auth/accept-invite",
            new AcceptInviteRequest(invitedUser.InviteToken, "password456"));

        secondResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AcceptInvite_With_Unknown_Token_Should_Return_BadRequest()
    {
        HttpResponseMessage response = await AppHttpClient.PostAsJsonAsync(
            "/api/auth/accept-invite",
            new AcceptInviteRequest("unknown-token", "password123"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AcceptInvite_With_Expired_Token_Should_Return_BadRequest()
    {
        Guid companyId = Guid.NewGuid();
        ApplicationUser admin = await CreateIdentityUserAsync(
            "invite-expired-admin@example.com",
            "inviteexpiredadmin",
            "Invite Expired Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);
        ApplicationUser targetUser = await CreatePendingIdentityUserAsync(
            "invite-expired-user@example.com",
            "inviteexpireduser",
            "Invite Expired User",
            companyId,
            AuthRoles.VIEWER);

        string rawInviteToken = "expired-invite-token";
        await AddExpiredInviteTokenAsync(targetUser.Id, admin.Id, rawInviteToken);

        HttpResponseMessage response = await AppHttpClient.PostAsJsonAsync(
            "/api/auth/accept-invite",
            new AcceptInviteRequest(rawInviteToken, "password123"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        ApplicationUser savedUser = await ExecuteInDb(dbContext => dbContext.Users
            .SingleAsync(user => user.Id == targetUser.Id));
        savedUser.IsActive.Should().BeFalse();
        savedUser.PasswordHash.Should().BeNull();
    }

    [Fact]
    public async Task ResendInvite_For_Pending_User_Should_Revoke_Active_Token_And_Create_New_Token()
    {
        Guid companyId = Guid.NewGuid();
        await CreateIdentityUserAsync(
            "resend-invite-admin@example.com",
            "resendinviteadmin",
            "Resend Invite Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);
        TokenResponse login = await LoginAsync("resend-invite-admin@example.com");

        InviteUserResponse invitedUser = await InviteUserAsync(
            login.AccessToken,
            new InviteUserRequest(
                "resend-invite-user@example.com",
                "resendinviteuser",
                "Resend Invite User",
                companyId,
                AuthRoles.VIEWER));

        using HttpRequestMessage request = new(HttpMethod.Post, $"/api/users/{invitedUser.UserId}/resend-invite");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        Envelope<ResendInviteResponse>? envelope =
            await response.Content.ReadFromJsonAsync<Envelope<ResendInviteResponse>>();

        envelope.Should().NotBeNull();
        envelope!.Result.Should().NotBeNull();

        ResendInviteResponse resend = envelope.Result!;
        resend.UserId.Should().Be(invitedUser.UserId);
        resend.Email.Should().Be("resend-invite-user@example.com");
        resend.InviteToken.Should().NotBeNullOrWhiteSpace();
        resend.InviteToken.Should().NotBe(invitedUser.InviteToken);
        resend.InviteTokenExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(3), TimeSpan.FromMinutes(1));

        List<UserInviteToken> inviteTokens = await ExecuteInDb(dbContext => dbContext.UserInviteTokens
            .Where(token => token.UserId == invitedUser.UserId)
            .OrderBy(token => token.CreatedAt)
            .ToListAsync());

        inviteTokens.Should().HaveCount(2);
        inviteTokens[0].RevokedAt.Should().NotBeNull();
        inviteTokens[1].RevokedAt.Should().BeNull();
        inviteTokens[1].AcceptedAt.Should().BeNull();

        HttpResponseMessage oldTokenResponse = await AppHttpClient.PostAsJsonAsync(
            "/api/auth/accept-invite",
            new AcceptInviteRequest(invitedUser.InviteToken, "password123"));
        oldTokenResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        HttpResponseMessage newTokenResponse = await AppHttpClient.PostAsJsonAsync(
            "/api/auth/accept-invite",
            new AcceptInviteRequest(resend.InviteToken, "password123"));
        newTokenResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResendInvite_For_Active_User_Should_Return_BadRequest()
    {
        Guid companyId = Guid.NewGuid();
        await CreateIdentityUserAsync(
            "resend-active-admin@example.com",
            "resendactiveadmin",
            "Resend Active Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);
        ApplicationUser targetUser = await CreateIdentityUserAsync(
            "resend-active-user@example.com",
            "resendactiveuser",
            "Resend Active User",
            companyId,
            AuthRoles.VIEWER);
        TokenResponse login = await LoginAsync("resend-active-admin@example.com");

        using HttpRequestMessage request = new(HttpMethod.Post, $"/api/users/{targetUser.Id}/resend-invite");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ResendInvite_To_Another_Company_By_CompanyAdmin_Should_Return_NotFound()
    {
        Guid companyId = Guid.NewGuid();
        await CreateIdentityUserAsync(
            "resend-company-admin@example.com",
            "resendcompanyadmin",
            "Resend Company Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);
        ApplicationUser targetUser = await CreatePendingIdentityUserAsync(
            "resend-other-company-user@example.com",
            "resendothercompanyuser",
            "Resend Other Company User",
            Guid.NewGuid(),
            AuthRoles.VIEWER);
        TokenResponse login = await LoginAsync("resend-company-admin@example.com");

        using HttpRequestMessage request = new(HttpMethod.Post, $"/api/users/{targetUser.Id}/resend-invite");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task InviteUser_Without_Access_Token_Should_Return_Unauthorized()
    {
        InviteUserRequest inviteRequest = new(
            "anonymous-invite@example.com",
            "anonymousinvite",
            null,
            Guid.NewGuid(),
            AuthRoles.VIEWER);

        HttpResponseMessage response = await AppHttpClient.PostAsJsonAsync(
            "/api/users/invite",
            inviteRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task InviteUser_Without_UsersManage_Permission_Should_Return_Forbidden()
    {
        Guid companyId = Guid.NewGuid();
        await CreateIdentityUserAsync(
            "invite-viewer@example.com",
            "inviteviewer",
            "Invite Viewer",
            companyId,
            AuthRoles.VIEWER);
        TokenResponse login = await LoginAsync("invite-viewer@example.com");

        InviteUserRequest inviteRequest = new(
            "viewer-invite-target@example.com",
            "viewerinvitetarget",
            null,
            companyId,
            AuthRoles.VIEWER);

        using HttpRequestMessage request = new(HttpMethod.Post, "/api/users/invite")
        {
            Content = JsonContent.Create(inviteRequest)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task InviteUser_To_Another_Company_By_CompanyAdmin_Should_Return_BadRequest()
    {
        Guid companyId = Guid.NewGuid();
        await CreateIdentityUserAsync(
            "invite-company-admin@example.com",
            "invitecompanyadmin",
            "Invite Company Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);
        TokenResponse login = await LoginAsync("invite-company-admin@example.com");

        InviteUserRequest inviteRequest = new(
            "another-company-user@example.com",
            "anothercompanyuser",
            null,
            Guid.NewGuid(),
            AuthRoles.VIEWER);

        using HttpRequestMessage request = new(HttpMethod.Post, "/api/users/invite")
        {
            Content = JsonContent.Create(inviteRequest)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task InviteUser_To_Another_Company_By_SystemAdmin_Should_Create_Identity_User()
    {
        await CreateIdentityUserAsync(
            "invite-system-admin@example.com",
            "invitesystemadmin",
            "Invite System Admin",
            Guid.NewGuid(),
            AuthRoles.SYSTEM_ADMIN);
        TokenResponse login = await LoginAsync("invite-system-admin@example.com");

        Guid targetCompanyId = Guid.NewGuid();
        InviteUserRequest inviteRequest = new(
            "system-admin-target@example.com",
            "systemadmintarget",
            null,
            targetCompanyId,
            AuthRoles.COMPANY_ADMIN);

        using HttpRequestMessage request = new(HttpMethod.Post, "/api/users/invite")
        {
            Content = JsonContent.Create(inviteRequest)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        Envelope<InviteUserResponse>? envelope =
            await response.Content.ReadFromJsonAsync<Envelope<InviteUserResponse>>();

        envelope.Should().NotBeNull();
        envelope!.Result.Should().NotBeNull();
        envelope.Result!.CompanyId.Should().Be(targetCompanyId);
        envelope.Result.Roles.Should().BeEquivalentTo(AuthRoles.COMPANY_ADMIN);
        envelope.Result.InviteToken.Should().NotBeNullOrWhiteSpace();
    }

    private async Task AssertUserCanLoginAsync(string email, string password)
    {
        HttpResponseMessage response = await AppHttpClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, password));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<InviteUserResponse> InviteUserAsync(string accessToken, InviteUserRequest inviteRequest)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/users/invite")
        {
            Content = JsonContent.Create(inviteRequest)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        Envelope<InviteUserResponse>? envelope =
            await response.Content.ReadFromJsonAsync<Envelope<InviteUserResponse>>();

        envelope.Should().NotBeNull();
        envelope!.Result.Should().NotBeNull();

        return envelope.Result!;
    }

    private async Task AddExpiredInviteTokenAsync(Guid userId, Guid createdByUserId, string rawInviteToken)
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();

        AuthServiceDbContext dbContext = scope.ServiceProvider.GetRequiredService<AuthServiceDbContext>();
        ITokenService tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        UserInviteToken inviteToken = UserInviteToken.Create(
            userId,
            createdByUserId,
            tokenService.HashRefreshToken(rawInviteToken),
            DateTime.UtcNow.AddSeconds(1)).Value;

        dbContext.UserInviteTokens.Add(inviteToken);
        await dbContext.SaveChangesAsync();

        await Task.Delay(TimeSpan.FromSeconds(2));
    }

    private async Task AssertUserCannotLoginAsync(string email, string password)
    {
        HttpResponseMessage response = await AppHttpClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, password));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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

    private async Task<ApplicationUser> CreatePendingIdentityUserAsync(
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
        user.Deactivate();

        IdentityResult createResult = await userManager.CreateAsync(user);
        createResult.Succeeded.Should().BeTrue(string.Join("; ", createResult.Errors.Select(error => error.Description)));

        IdentityResult roleResult = await userManager.AddToRoleAsync(user, role);
        roleResult.Succeeded.Should().BeTrue(string.Join("; ", roleResult.Errors.Select(error => error.Description)));

        return user;
    }
}
