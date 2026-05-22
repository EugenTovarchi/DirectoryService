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

public sealed class AuthAuditTests : AuthServiceBaseTests
{
    public AuthAuditTests(AuthServiceTestWebFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Invite_And_AcceptInvite_Should_Write_Audit_Events()
    {
        Guid companyId = Guid.NewGuid();
        await CreateIdentityUserAsync(
            "audit-invite-admin@example.com",
            "auditinviteadmin",
            "Audit Invite Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);
        TokenResponse login = await LoginAsync("audit-invite-admin@example.com");

        InviteUserResponse invitedUser = await InviteUserAsync(
            login.AccessToken,
            new InviteUserRequest(
                "audit-invite-user@example.com",
                "auditinviteuser",
                "Audit Invite User",
                companyId,
                AuthRoles.VIEWER));

        string inviteToken = GetLatestInviteTokenForUser(invitedUser.UserId);
        HttpResponseMessage acceptResponse = await AppHttpClient.PostAsJsonAsync(
            "/api/auth/accept-invite",
            new AcceptInviteRequest(inviteToken, "password123"));
        acceptResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        string[] actions = await GetAuditActionsForUserAsync(invitedUser.UserId);
        actions.Should().Contain(AuthAuditActions.INVITE_CREATED);
        actions.Should().Contain(AuthAuditActions.INVITE_ACCEPTED);
    }

    [Fact]
    public async Task PasswordReset_Should_Write_Audit_Events()
    {
        ApplicationUser user = await CreateIdentityUserAsync(
            "audit-reset-user@example.com",
            "auditresetuser",
            "Audit Reset User",
            Guid.NewGuid(),
            AuthRoles.VIEWER);

        HttpResponseMessage requestResponse = await AppHttpClient.PostAsJsonAsync(
            "/api/auth/request-password-reset",
            new RequestPasswordResetRequest("audit-reset-user@example.com"));
        requestResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        string resetToken = GetLatestPasswordResetTokenForUser(user.Id);
        HttpResponseMessage resetResponse = await AppHttpClient.PostAsJsonAsync(
            "/api/auth/reset-password",
            new ResetPasswordRequest(resetToken, "newpassword123"));
        resetResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        string[] actions = await GetAuditActionsForUserAsync(user.Id);
        actions.Should().Contain(AuthAuditActions.PASSWORD_RESET_REQUESTED);
        actions.Should().Contain(AuthAuditActions.PASSWORD_RESET_COMPLETED);
    }

    [Fact]
    public async Task Admin_User_Management_Should_Write_Audit_Events()
    {
        Guid companyId = Guid.NewGuid();
        await CreateIdentityUserAsync(
            "audit-admin@example.com",
            "auditadmin",
            "Audit Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);
        ApplicationUser targetUser = await CreateIdentityUserAsync(
            "audit-target@example.com",
            "audittarget",
            "Audit Target",
            companyId,
            AuthRoles.VIEWER);
        await LoginAsync("audit-target@example.com");

        TokenResponse login = await LoginAsync("audit-admin@example.com");

        await SendAuthorizedPatchAsync(
            $"/api/users/{targetUser.Id}/profile",
            login.AccessToken,
            new UpdateUserProfileRequest("Audit Target Updated"));

        await SendAuthorizedPatchAsync(
            $"/api/users/{targetUser.Id}/change-role",
            login.AccessToken,
            new ChangeUserRoleRequest(AuthRoles.OPERATOR));

        await SendAuthorizedPatchAsync(
            $"/api/users/{targetUser.Id}/change-status",
            login.AccessToken,
            new ChangeUserStatusRequest(false));

        using HttpRequestMessage revokeRequest = new(HttpMethod.Post, $"/api/users/{targetUser.Id}/revoke-sessions");
        revokeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        HttpResponseMessage revokeResponse = await AppHttpClient.SendAsync(revokeRequest);
        revokeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        string[] actions = await GetAuditActionsForUserAsync(targetUser.Id);
        actions.Should().Contain(AuthAuditActions.USER_PROFILE_CHANGED);
        actions.Should().Contain(AuthAuditActions.USER_ROLE_CHANGED);
        actions.Should().Contain(AuthAuditActions.USER_STATUS_CHANGED);
        actions.Should().Contain(AuthAuditActions.ALL_SESSIONS_REVOKED);
    }

    private async Task SendAuthorizedPatchAsync<TRequest>(
        string requestUri,
        string accessToken,
        TRequest body)
    {
        using HttpRequestMessage request = new(HttpMethod.Patch, requestUri)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private Task<string[]> GetAuditActionsForUserAsync(Guid userId)
    {
        return ExecuteInDb(dbContext => dbContext.AuthAuditEvents
            .Where(auditEvent => auditEvent.UserId == userId)
            .OrderBy(auditEvent => auditEvent.CreatedAt)
            .Select(auditEvent => auditEvent.Action)
            .ToArrayAsync());
    }

    private string GetLatestInviteTokenForUser(Guid userId)
    {
        TestInviteEmailSender emailSender = Services.GetRequiredService<TestInviteEmailSender>();
        Uri inviteLink = emailSender.Messages
            .Where(message => message.UserId == userId)
            .Select(message => message.InviteLink)
            .Last();

        return ExtractToken(inviteLink);
    }

    private string GetLatestPasswordResetTokenForUser(Guid userId)
    {
        TestPasswordResetEmailSender emailSender = Services.GetRequiredService<TestPasswordResetEmailSender>();
        Uri resetLink = emailSender.Messages
            .Where(message => message.UserId == userId)
            .Select(message => message.ResetLink)
            .Last();

        return ExtractToken(resetLink);
    }

    private static string ExtractToken(Uri link)
    {
        string query = link.Query.TrimStart('?');
        string? tokenPair = query
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(part => part.StartsWith("token=", StringComparison.Ordinal));

        tokenPair.Should().NotBeNull();

        return Uri.UnescapeDataString(tokenPair!["token=".Length..]);
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
