using System.Net;
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

public sealed class PasswordResetTests : AuthServiceBaseTests
{
    public PasswordResetTests(AuthServiceTestWebFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task PasswordReset_For_Active_User_Should_Send_Link_And_Replace_Password()
    {
        ApplicationUser user = await CreateIdentityUserAsync(
            "password-reset-user@example.com",
            "passwordresetuser",
            "Password Reset User",
            Guid.NewGuid(),
            AuthRoles.VIEWER);

        HttpResponseMessage requestResponse = await AppHttpClient.PostAsJsonAsync(
            "/api/auth/request-password-reset",
            new RequestPasswordResetRequest("password-reset-user@example.com"));

        requestResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        string resetToken = GetLatestPasswordResetTokenForUser(user.Id);
        resetToken.Should().NotBeNullOrWhiteSpace();

        int tokenCount = await ExecuteInDb(dbContext => dbContext.PasswordResetTokens
            .CountAsync(token => token.UserId == user.Id));
        tokenCount.Should().Be(1);

        HttpResponseMessage resetResponse = await AppHttpClient.PostAsJsonAsync(
            "/api/auth/reset-password",
            new ResetPasswordRequest(resetToken, "newpassword123"));

        resetResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        PasswordResetToken savedToken = await ExecuteInDb(dbContext => dbContext.PasswordResetTokens
            .SingleAsync(token => token.UserId == user.Id));
        savedToken.UsedAt.Should().NotBeNull();

        await AssertUserCannotLoginAsync("password-reset-user@example.com", "password123");
        await AssertUserCanLoginAsync("password-reset-user@example.com", "newpassword123");
    }

    [Fact]
    public async Task RequestPasswordReset_With_Unknown_Email_Should_Return_Ok_Without_Email()
    {
        int emailCountBefore = Services.GetRequiredService<TestPasswordResetEmailSender>().Messages.Count;

        HttpResponseMessage response = await AppHttpClient.PostAsJsonAsync(
            "/api/auth/request-password-reset",
            new RequestPasswordResetRequest("unknown-reset@example.com"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        int emailCountAfter = Services.GetRequiredService<TestPasswordResetEmailSender>().Messages.Count;
        emailCountAfter.Should().Be(emailCountBefore);
    }

    [Fact]
    public async Task ResetPassword_With_Unknown_Token_Should_Return_BadRequest()
    {
        HttpResponseMessage response = await AppHttpClient.PostAsJsonAsync(
            "/api/auth/reset-password",
            new ResetPasswordRequest("unknown-token", "newpassword123"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ResetPassword_With_Used_Token_Should_Return_BadRequest()
    {
        ApplicationUser user = await CreateIdentityUserAsync(
            "password-reset-reuse@example.com",
            "passwordresetreuse",
            "Password Reset Reuse",
            Guid.NewGuid(),
            AuthRoles.VIEWER);

        await AppHttpClient.PostAsJsonAsync(
            "/api/auth/request-password-reset",
            new RequestPasswordResetRequest("password-reset-reuse@example.com"));
        string resetToken = GetLatestPasswordResetTokenForUser(user.Id);

        HttpResponseMessage firstResponse = await AppHttpClient.PostAsJsonAsync(
            "/api/auth/reset-password",
            new ResetPasswordRequest(resetToken, "newpassword123"));
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage secondResponse = await AppHttpClient.PostAsJsonAsync(
            "/api/auth/reset-password",
            new ResetPasswordRequest(resetToken, "anotherpassword123"));

        secondResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RequestPasswordReset_Twice_Should_Revoke_Previous_Token()
    {
        ApplicationUser user = await CreateIdentityUserAsync(
            "password-reset-twice@example.com",
            "passwordresettwice",
            "Password Reset Twice",
            Guid.NewGuid(),
            AuthRoles.VIEWER);

        await AppHttpClient.PostAsJsonAsync(
            "/api/auth/request-password-reset",
            new RequestPasswordResetRequest("password-reset-twice@example.com"));
        string firstResetToken = GetLatestPasswordResetTokenForUser(user.Id);

        await AppHttpClient.PostAsJsonAsync(
            "/api/auth/request-password-reset",
            new RequestPasswordResetRequest("password-reset-twice@example.com"));
        string secondResetToken = GetLatestPasswordResetTokenForUser(user.Id);

        secondResetToken.Should().NotBe(firstResetToken);

        List<PasswordResetToken> savedTokens = await ExecuteInDb(dbContext => dbContext.PasswordResetTokens
            .Where(token => token.UserId == user.Id)
            .OrderBy(token => token.CreatedAt)
            .ToListAsync());

        savedTokens.Should().HaveCount(2);
        savedTokens[0].RevokedAt.Should().NotBeNull();
        savedTokens[1].RevokedAt.Should().BeNull();

        HttpResponseMessage oldTokenResponse = await AppHttpClient.PostAsJsonAsync(
            "/api/auth/reset-password",
            new ResetPasswordRequest(firstResetToken, "newpassword123"));
        oldTokenResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        HttpResponseMessage newTokenResponse = await AppHttpClient.PostAsJsonAsync(
            "/api/auth/reset-password",
            new ResetPasswordRequest(secondResetToken, "newpassword123"));
        newTokenResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetPassword_With_Expired_Token_Should_Return_BadRequest()
    {
        ApplicationUser user = await CreateIdentityUserAsync(
            "password-reset-expired@example.com",
            "passwordresetexpired",
            "Password Reset Expired",
            Guid.NewGuid(),
            AuthRoles.VIEWER);
        string rawResetToken = "expired-password-reset-token";
        await AddExpiredPasswordResetTokenAsync(user.Id, rawResetToken);

        HttpResponseMessage response = await AppHttpClient.PostAsJsonAsync(
            "/api/auth/reset-password",
            new ResetPasswordRequest(rawResetToken, "newpassword123"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await AssertUserCanLoginAsync("password-reset-expired@example.com", "password123");
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

    private static string ExtractToken(Uri resetLink)
    {
        string query = resetLink.Query.TrimStart('?');
        string? tokenPair = query
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(part => part.StartsWith("token=", StringComparison.Ordinal));

        tokenPair.Should().NotBeNull();

        return Uri.UnescapeDataString(tokenPair!["token=".Length..]);
    }

    private async Task AddExpiredPasswordResetTokenAsync(Guid userId, string rawResetToken)
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();

        AuthServiceDbContext dbContext = scope.ServiceProvider.GetRequiredService<AuthServiceDbContext>();
        ITokenService tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        PasswordResetToken resetToken = PasswordResetToken.Create(
            userId,
            tokenService.HashRefreshToken(rawResetToken),
            DateTime.UtcNow.AddSeconds(1)).Value;

        dbContext.PasswordResetTokens.Add(resetToken);
        await dbContext.SaveChangesAsync();

        await Task.Delay(TimeSpan.FromSeconds(2));
    }

    private async Task AssertUserCanLoginAsync(string email, string password)
    {
        HttpResponseMessage response = await AppHttpClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, password));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task AssertUserCannotLoginAsync(string email, string password)
    {
        HttpResponseMessage response = await AppHttpClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, password));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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
