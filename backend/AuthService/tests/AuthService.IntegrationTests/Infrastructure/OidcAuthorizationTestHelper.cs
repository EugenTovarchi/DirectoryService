using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AuthService.Infrastructure.Postgres.Seeding;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace AuthService.IntegrationTests.Infrastructure;

/// <summary>
/// Получает пользовательский OpenIddict access token через Authorization Code + PKCE.
/// Helper не запрашивает offline_access, поэтому служебная авторизация теста
/// не создаёт активную session с refresh token.
/// </summary>
public static class OidcAuthorizationTestHelper
{
    private const string CLIENT_ID = "oidc-public-tests";
    private const string REDIRECT_URI = "https://public-client.tests/callback";
    private const string CODE_VERIFIER =
        "oidc-test-code-verifier-with-more-than-forty-three-characters-123456789";

    /// <summary>
    /// Выполняет интерактивный OIDC flow и возвращает access token.
    /// Невалидные credentials возвращают null без раскрытия причины отказа.
    /// </summary>
    public static async Task<OidcTestToken?> TryLoginAsync(
        AuthServiceTestWebFactory factory,
        IServiceProvider services,
        string email,
        string password,
        string? userAgent = null)
    {
        await SeedOidcClientAsync(services);

        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://auth-service.tests")
            });
        if (!string.IsNullOrWhiteSpace(userAgent))
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", userAgent);
        }

        string authorizationUrl = CreateAuthorizationUrl();
        using HttpResponseMessage anonymousAuthorizationResponse =
            await client.GetAsync(authorizationUrl);
        EnsureStatus(
            anonymousAuthorizationResponse,
            HttpStatusCode.Redirect,
            "OIDC authorization did not redirect to login");

        string loginUrl = anonymousAuthorizationResponse.Headers.Location?.OriginalString
            ?? throw new InvalidOperationException("OIDC login redirect is missing");
        using HttpResponseMessage loginPageResponse = await client.GetAsync(loginUrl);
        EnsureStatus(loginPageResponse, HttpStatusCode.OK, "OIDC login page is unavailable");
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
        if (loginResponse.StatusCode == HttpStatusCode.OK)
        {
            return null;
        }

        EnsureStatus(loginResponse, HttpStatusCode.Redirect, "OIDC browser login failed");

        using HttpResponseMessage consentPageResponse = await client.GetAsync(authorizationUrl);
        EnsureStatus(consentPageResponse, HttpStatusCode.OK, "OIDC consent page is unavailable");
        string consentAntiforgeryToken = await ReadAntiforgeryTokenAsync(consentPageResponse);

        var consentForm = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["decision"] = "accept",
            ["__RequestVerificationToken"] = consentAntiforgeryToken,
            ["client_id"] = CLIENT_ID,
            ["redirect_uri"] = REDIRECT_URI,
            ["response_type"] = "code",
            ["scope"] = "openid auth",
            ["code_challenge"] = CreateCodeChallenge(),
            ["code_challenge_method"] = "S256",
            ["state"] = "test-state"
        };
        using HttpResponseMessage authorizationResponse = await client.PostAsync(
            "/connect/authorize",
            new FormUrlEncodedContent(consentForm));
        EnsureStatus(
            authorizationResponse,
            HttpStatusCode.Redirect,
            "OIDC authorization code was not returned");

        Uri callbackUri = authorizationResponse.Headers.Location
            ?? throw new InvalidOperationException("OIDC authorization callback is missing");
        string authorizationCode = QueryHelpers
            .ParseQuery(callbackUri.Query)["code"]
            .ToString();
        if (string.IsNullOrWhiteSpace(authorizationCode))
        {
            throw new InvalidOperationException("OIDC authorization code is missing");
        }

        var tokenForm = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = CLIENT_ID,
            ["redirect_uri"] = REDIRECT_URI,
            ["code"] = authorizationCode,
            ["code_verifier"] = CODE_VERIFIER
        };
        using HttpResponseMessage tokenResponse = await client.PostAsync(
            "/connect/token",
            new FormUrlEncodedContent(tokenForm));
        EnsureStatus(tokenResponse, HttpStatusCode.OK, "OIDC token exchange failed");

        using JsonDocument tokenDocument = JsonDocument.Parse(
            await tokenResponse.Content.ReadAsStringAsync());
        string accessToken = tokenDocument.RootElement
            .GetProperty("access_token")
            .GetString()
            ?? throw new InvalidOperationException("OIDC access token is missing");

        return new OidcTestToken(accessToken);
    }

    private static async Task SeedOidcClientAsync(IServiceProvider services)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        OidcServerSeeder seeder = scope.ServiceProvider.GetRequiredService<OidcServerSeeder>();
        await seeder.SeedAsync();
    }

    private static string CreateAuthorizationUrl()
    {
        var parameters = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["client_id"] = CLIENT_ID,
            ["redirect_uri"] = REDIRECT_URI,
            ["response_type"] = "code",
            ["scope"] = "openid auth",
            ["code_challenge"] = CreateCodeChallenge(),
            ["code_challenge_method"] = "S256",
            ["state"] = "test-state"
        };

        return QueryHelpers.AddQueryString("/connect/authorize", parameters);
    }

    private static string CreateCodeChallenge()
    {
        return WebEncoders.Base64UrlEncode(
            SHA256.HashData(Encoding.ASCII.GetBytes(CODE_VERIFIER)));
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

    private static void EnsureStatus(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string errorMessage)
    {
        if (response.StatusCode != expectedStatus)
        {
            throw new InvalidOperationException(
                $"{errorMessage}. Status: {response.StatusCode}");
        }
    }
}

/// <summary>
/// Минимальная test-only проекция OpenIddict token response.
/// </summary>
public sealed record OidcTestToken(string AccessToken);
