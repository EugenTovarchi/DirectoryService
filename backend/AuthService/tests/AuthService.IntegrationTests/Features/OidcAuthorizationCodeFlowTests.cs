using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AuthService.Contracts.Responses;
using AuthService.Domain.Identity;
using AuthService.Infrastructure.Postgres.Seeding;
using AuthService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using OpenIddict.Abstractions;
using SharedService.SharedKernel;

namespace AuthService.IntegrationTests.Features;

public sealed class OidcAuthorizationCodeFlowTests : AuthServiceBaseTests
{
    private readonly AuthServiceTestWebFactory _factory;

    public OidcAuthorizationCodeFlowTests(AuthServiceTestWebFactory factory)
        : base(factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AuthorizationCodeFlow_With_Pkce_Should_Return_Tokens()
    {
        // Arrange
        const string email = "oidc-code-user@24eye.test";
        const string password = "password123";
        const string clientId = "oidc-public-tests";
        const string redirectUri = "https://public-client.tests/callback";
        const string codeVerifier =
            "oidc-test-code-verifier-with-more-than-forty-three-characters-123456789";

        await SeedOidcAndCreateUserAsync(email, password);

        string codeChallenge = WebEncoders.Base64UrlEncode(
            SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));
        var authorizationParameters = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = "openid profile email offline_access directory files auth",
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
            ["state"] = "test-state"
        };
        string authorizationUrl = QueryHelpers.AddQueryString(
            "/connect/authorize",
            authorizationParameters);

        using HttpClient client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://auth-service.tests")
            });

        // Act
        using HttpResponseMessage anonymousAuthorizationResponse =
            await client.GetAsync(authorizationUrl);

        string loginUrl = anonymousAuthorizationResponse.Headers.Location?.OriginalString
            ?? throw new InvalidOperationException("Login redirect is missing");
        using HttpResponseMessage loginPageResponse = await client.GetAsync(loginUrl);
        string loginAntiforgeryToken = await ReadAntiforgeryTokenAsync(loginPageResponse);

        var loginForm = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Email"] = email,
            ["Password"] = password,
            ["ReturnUrl"] = authorizationUrl,
            ["__RequestVerificationToken"] = loginAntiforgeryToken
        };
        using HttpResponseMessage loginResponse = await client.PostAsync(
            "/connect/login",
            new FormUrlEncodedContent(loginForm));

        using HttpResponseMessage consentPageResponse = await client.GetAsync(authorizationUrl);
        string consentAntiforgeryToken = await ReadAntiforgeryTokenAsync(consentPageResponse);

        var consentForm = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["decision"] = "accept",
            ["__RequestVerificationToken"] = consentAntiforgeryToken,
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = "openid profile email offline_access directory files auth",
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
            ["state"] = "test-state"
        };
        using HttpResponseMessage authorizationResponse = await client.PostAsync(
            "/connect/authorize",
            new FormUrlEncodedContent(consentForm));

        Uri? callbackLocation = authorizationResponse.Headers.Location;
        if (callbackLocation is null)
        {
            string responseBody = await authorizationResponse.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Authorization callback is missing. " +
                $"Status: {authorizationResponse.StatusCode}. Body: {responseBody}");
        }

        Uri callbackUri = callbackLocation;
        string authorizationCode = QueryHelpers.ParseQuery(callbackUri.Query)["code"].ToString();

        var tokenForm = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["code"] = authorizationCode,
            ["code_verifier"] = codeVerifier
        };
        using HttpResponseMessage tokenResponse = await client.PostAsync(
            "/connect/token",
            new FormUrlEncodedContent(tokenForm));
        string tokenResponseBody = await tokenResponse.Content.ReadAsStringAsync();
        using JsonDocument tokenDocument = JsonDocument.Parse(tokenResponseBody);
        if (!tokenDocument.RootElement.TryGetProperty(
                "access_token",
                out JsonElement accessTokenElement))
        {
            throw new InvalidOperationException("Access token is missing");
        }

        string accessToken = accessTokenElement
            .GetString()
            ?? throw new InvalidOperationException("Access token is missing");
        string idToken = tokenDocument.RootElement
            .GetProperty("id_token")
            .GetString()
            ?? throw new InvalidOperationException("ID token is missing");
        string refreshToken = tokenDocument.RootElement
            .GetProperty("refresh_token")
            .GetString()
            ?? throw new InvalidOperationException("Refresh token is missing");
        var accessJsonWebToken = new JsonWebToken(accessToken);
        var idJsonWebToken = new JsonWebToken(idToken);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        using HttpResponseMessage userInfoResponse = await client.GetAsync(
            "/connect/userinfo");
        using JsonDocument userInfoDocument = JsonDocument.Parse(
            await userInfoResponse.Content.ReadAsStringAsync());

        using HttpResponseMessage currentUserResponse = await client.GetAsync(
            "/api/auth/me");
        Envelope<CurrentUserResponse>? currentUserEnvelope =
            await currentUserResponse.Content
                .ReadFromJsonAsync<Envelope<CurrentUserResponse>>();
        using HttpResponseMessage usersManageResponse = await client.GetAsync(
            "/api/users");

        var refreshForm = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = clientId,
            ["refresh_token"] = refreshToken
        };
        using HttpResponseMessage refreshResponse = await client.PostAsync(
            "/connect/token",
            new FormUrlEncodedContent(refreshForm));
        using JsonDocument refreshDocument = JsonDocument.Parse(
            await refreshResponse.Content.ReadAsStringAsync());
        string rotatedRefreshToken = refreshDocument.RootElement
            .GetProperty("refresh_token")
            .GetString()
            ?? throw new InvalidOperationException("Rotated refresh token is missing");

        var revocationForm = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["client_id"] = clientId,
            ["token"] = rotatedRefreshToken,
            ["token_type_hint"] = OpenIddictConstants.TokenTypeHints.RefreshToken
        };
        using HttpResponseMessage revocationResponse = await client.PostAsync(
            "/connect/revoke",
            new FormUrlEncodedContent(revocationForm));

        var revokedRefreshForm = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = clientId,
            ["refresh_token"] = rotatedRefreshToken
        };
        using HttpResponseMessage revokedRefreshTokenResponse = await client.PostAsync(
            "/connect/token",
            new FormUrlEncodedContent(revokedRefreshForm));
        using JsonDocument revokedRefreshTokenDocument = JsonDocument.Parse(
            await revokedRefreshTokenResponse.Content.ReadAsStringAsync());

        using HttpResponseMessage reusedRefreshTokenResponse = await client.PostAsync(
            "/connect/token",
            new FormUrlEncodedContent(refreshForm));
        using JsonDocument reusedRefreshTokenDocument = JsonDocument.Parse(
            await reusedRefreshTokenResponse.Content.ReadAsStringAsync());

        // Assert
        anonymousAuthorizationResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        loginPageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        consentPageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        authorizationResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        callbackUri.GetLeftPart(UriPartial.Path).Should().Be(redirectUri);
        QueryHelpers.ParseQuery(callbackUri.Query)["state"].ToString().Should().Be("test-state");
        authorizationCode.Should().NotBeNullOrWhiteSpace();

        tokenResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        tokenDocument.RootElement.GetProperty("access_token").GetString()
            .Should().NotBeNullOrWhiteSpace();
        tokenDocument.RootElement.GetProperty("id_token").GetString()
            .Should().NotBeNullOrWhiteSpace();
        tokenDocument.RootElement.GetProperty("refresh_token").GetString()
            .Should().NotBeNullOrWhiteSpace();

        userInfoResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        userInfoDocument.RootElement.GetProperty("sub").GetString()
            .Should().NotBeNullOrWhiteSpace();
        userInfoDocument.RootElement.GetProperty("name").GetString()
            .Should().Be("OIDC Code User");
        userInfoDocument.RootElement.GetProperty("preferred_username").GetString()
            .Should().Be(email);
        userInfoDocument.RootElement.GetProperty("email").GetString()
            .Should().Be(email);
        userInfoDocument.RootElement.GetProperty("email_verified").GetBoolean()
            .Should().BeFalse();
        userInfoDocument.RootElement.GetProperty("company_id").GetString()
            .Should().Be("927d7bbe-9c84-4c08-a797-cfcb8922b756");
        userInfoDocument.RootElement.GetProperty("roles")
            .EnumerateArray()
            .Select(value => value.GetString())
            .Should()
            .Contain(AuthRoles.VIEWER);
        userInfoDocument.RootElement.GetProperty("permissions")
            .EnumerateArray()
            .Select(value => value.GetString())
            .Should()
            .Contain(new[]
            {
                AuthPermissions.DIRECTORY_READ,
                AuthPermissions.FILES_READ
            });

        currentUserResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        currentUserEnvelope.Should().NotBeNull();
        currentUserEnvelope!.Result.Should().NotBeNull();
        currentUserEnvelope.Result!.Email.Should().Be(email);
        currentUserEnvelope.Result.Roles.Should().Contain(AuthRoles.VIEWER);
        usersManageResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        accessJsonWebToken.Audiences.Should().Contain(new[]
        {
            "directory-service",
            "file-service",
            "auth-service"
        });
        accessJsonWebToken.Claims
            .Where(claim => claim.Type == AuthClaimTypes.PERMISSION)
            .Select(claim => claim.Value)
            .Should()
            .Contain(new[]
            {
                AuthPermissions.DIRECTORY_READ,
                AuthPermissions.FILES_READ
            });
        accessJsonWebToken.Claims
            .Where(claim => claim.Type == OpenIddictConstants.Claims.Role)
            .Select(claim => claim.Value)
            .Should()
            .Contain(AuthRoles.VIEWER);
        idJsonWebToken.Claims
            .Where(claim => claim.Type == OpenIddictConstants.Claims.Email)
            .Select(claim => claim.Value)
            .Should()
            .Contain(email);
        idJsonWebToken.Claims
            .Where(claim => claim.Type == AuthClaimTypes.PERMISSION)
            .Should()
            .BeEmpty();

        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        refreshDocument.RootElement.GetProperty("access_token").GetString()
            .Should().NotBeNullOrWhiteSpace();
        refreshDocument.RootElement.GetProperty("refresh_token").GetString()
            .Should().NotBeNullOrWhiteSpace()
            .And.NotBe(refreshToken);

        revocationResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        revokedRefreshTokenResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        revokedRefreshTokenDocument.RootElement.GetProperty("error").GetString()
            .Should().Be("invalid_grant");

        reusedRefreshTokenResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        reusedRefreshTokenDocument.RootElement.GetProperty("error").GetString()
            .Should().Be("invalid_grant");
    }

    private async Task SeedOidcAndCreateUserAsync(string email, string password)
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();

        var identitySeeder = scope.ServiceProvider.GetRequiredService<AuthIdentitySeeder>();
        await identitySeeder.SeedAsync();

        var oidcSeeder = scope.ServiceProvider.GetRequiredService<OidcServerSeeder>();
        await oidcSeeder.SeedAsync();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser(
            email,
            Username.Create(email).Value,
            DisplayName.Create("OIDC Code User").Value,
            currentCompanyId: Guid.Parse("927D7BBE-9C84-4C08-A797-CFCB8922B756"));
        IdentityResult result = await userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            string errors = string.Join(
                "; ",
                result.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"Failed to create OIDC test user: {errors}");
        }

        IdentityResult roleResult = await userManager.AddToRoleAsync(user, AuthRoles.VIEWER);
        if (!roleResult.Succeeded)
        {
            string errors = string.Join(
                "; ",
                roleResult.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"Failed to assign OIDC test role: {errors}");
        }
    }

    private static async Task<string> ReadAntiforgeryTokenAsync(HttpResponseMessage response)
    {
        string html = await response.Content.ReadAsStringAsync();
        Match tokenMatch = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"",
            RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
            TimeSpan.FromSeconds(1));

        return tokenMatch.Success
            ? tokenMatch.Groups["token"].Value
            : throw new InvalidOperationException("Antiforgery token was not rendered");
    }
}
