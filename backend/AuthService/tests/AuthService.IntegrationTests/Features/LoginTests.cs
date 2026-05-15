using System.IdentityModel.Tokens.Jwt;
using System.Net;
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

public sealed class LoginTests : AuthServiceBaseTests
{
    public LoginTests(AuthServiceTestWebFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Login_With_Valid_Credentials_Should_Return_Tokens_And_Save_Refresh_Token_Hash()
    {
        Guid companyId = Guid.NewGuid();
        var user = await CreateIdentityUserAsync(
            "viewer@example.com",
            "viewer",
            "Viewer User",
            companyId,
            AuthRoles.VIEWER);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest("viewer@example.com", "password123"))
        };
        request.Headers.UserAgent.ParseAdd("AuthServiceIntegrationTests/1.0");

        var response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<TokenResponse>>();
        var tokenResponse = envelope?.Result;

        tokenResponse.Should().NotBeNull();
        tokenResponse!.AccessToken.Should().NotBeNullOrWhiteSpace();
        tokenResponse.RefreshToken.Should().NotBeNullOrWhiteSpace();
        tokenResponse.AccessTokenExpiresAt.Should().BeAfter(DateTime.UtcNow);
        tokenResponse.RefreshTokenExpiresAt.Should().BeAfter(DateTime.UtcNow);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(tokenResponse.AccessToken);
        jwt.Claims.Should().Contain(claim => claim.Type == JwtRegisteredClaimNames.Sub && claim.Value == user.Id.ToString());
        jwt.Claims.Should().Contain(claim => claim.Type == JwtRegisteredClaimNames.Email && claim.Value == user.Email);
        jwt.Claims.Should().Contain(claim => claim.Type == JwtRegisteredClaimNames.Name && claim.Value == "Viewer User");
        jwt.Claims.Should().Contain(claim => claim.Type == AuthClaimTypes.COMPANY_ID && claim.Value == companyId.ToString());
        jwt.Claims.Should().Contain(claim => claim.Type == AuthClaimTypes.ROLE && claim.Value == AuthRoles.VIEWER);
        jwt.Claims.Should().Contain(claim => claim.Type == AuthClaimTypes.PERMISSION && claim.Value == AuthPermissions.DIRECTORY_READ);
        jwt.Claims.Should().Contain(claim => claim.Type == AuthClaimTypes.PERMISSION && claim.Value == AuthPermissions.FILES_READ);
        jwt.Claims.Should().Contain(claim => claim.Type == AuthClaimTypes.PERMISSION && claim.Value == AuthPermissions.VIDEOS_READ);

        var savedToken = await ExecuteInDb(dbContext => dbContext.RefreshTokens.SingleAsync());
        savedToken.UserId.Should().Be(user.Id);
        savedToken.TokenHash.Should().NotBe(tokenResponse.RefreshToken);
        savedToken.TokenHash.Should().HaveLength(64);
        savedToken.UserAgent.Should().Be("AuthServiceIntegrationTests/1.0");
    }

    [Fact]
    public async Task Login_With_Invalid_Password_Should_Return_BadRequest()
    {
        await CreateIdentityUserAsync(
            "operator@example.com",
            "operator",
            "Operator User",
            Guid.NewGuid(),
            AuthRoles.OPERATOR);

        var request = new LoginRequest("operator@example.com", "wrong-password");

        var response = await AppHttpClient.PostAsJsonAsync("/api/auth/login", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var tokenCount = await ExecuteInDb(dbContext => dbContext.RefreshTokens.CountAsync());
        tokenCount.Should().Be(0);
    }

    [Fact]
    public async Task Login_With_Inactive_User_Should_Return_BadRequest()
    {
        var user = await CreateIdentityUserAsync(
            "inactive@example.com",
            "inactive",
            "Inactive User",
            Guid.NewGuid(),
            AuthRoles.VIEWER);

        await ExecuteInDb(async dbContext =>
        {
            var trackedUser = await dbContext.Users.SingleAsync(identityUser => identityUser.Id == user.Id);
            trackedUser.Deactivate();
            await dbContext.SaveChangesAsync();
            return true;
        });

        var request = new LoginRequest("inactive@example.com", "password123");

        var response = await AppHttpClient.PostAsJsonAsync("/api/auth/login", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<ApplicationUser> CreateIdentityUserAsync(
        string email,
        string username,
        string displayName,
        Guid companyId,
        string role)
    {
        await using var scope = Services.CreateAsyncScope();

        var seeder = scope.ServiceProvider.GetRequiredService<AuthIdentitySeeder>();
        await seeder.SeedAsync();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser(
            email,
            Username.Create(username).Value,
            DisplayName.Create(displayName).Value,
            companyId);

        var createResult = await userManager.CreateAsync(user, "password123");
        createResult.Succeeded.Should().BeTrue(string.Join("; ", createResult.Errors.Select(error => error.Description)));

        var roleResult = await userManager.AddToRoleAsync(user, role);
        roleResult.Succeeded.Should().BeTrue(string.Join("; ", roleResult.Errors.Select(error => error.Description)));

        return user;
    }
}
