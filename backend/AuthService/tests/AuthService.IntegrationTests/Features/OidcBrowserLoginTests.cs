using System.Net;
using System.Text.RegularExpressions;
using AuthService.Domain.Identity;
using AuthService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace AuthService.IntegrationTests.Features;

public sealed class OidcBrowserLoginTests : AuthServiceBaseTests
{
    private readonly AuthServiceTestWebFactory _factory;

    public OidcBrowserLoginTests(AuthServiceTestWebFactory factory)
        : base(factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_With_Valid_Credentials_Should_Create_Secure_Browser_Cookie()
    {
        // Arrange
        const string email = "oidc-browser-user@24eye.test";
        const string password = "password123";
        const string returnUrl = "/connect/authorize?client_id=oidc-public-tests";
        await CreateIdentityUserAsync(email, password);

        using HttpClient client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://auth-service.tests")
            });

        using HttpResponseMessage loginPageResponse = await client.GetAsync(
            $"/connect/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        string loginPage = await loginPageResponse.Content.ReadAsStringAsync();
        Match tokenMatch = Regex.Match(
            loginPage,
            "name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"",
            RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
            TimeSpan.FromSeconds(1));
        if (!tokenMatch.Success)
            throw new InvalidOperationException("Antiforgery token was not rendered");

        var form = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Email"] = email,
            ["Password"] = password,
            ["ReturnUrl"] = returnUrl,
            ["__RequestVerificationToken"] = tokenMatch.Groups["token"].Value
        };

        // Act
        using HttpResponseMessage response = await client.PostAsync(
            "/connect/login",
            new FormUrlEncodedContent(form));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Be(returnUrl);

        string setCookie = response.Headers.GetValues("Set-Cookie").Single(
            value => value.StartsWith("__Host-24eye-oidc=", StringComparison.Ordinal));
        string normalizedCookie = setCookie.ToLowerInvariant();
        normalizedCookie.Should().Contain("path=/");
        normalizedCookie.Should().Contain("secure");
        normalizedCookie.Should().Contain("httponly");
        normalizedCookie.Should().Contain("samesite=lax");
    }

    [Fact]
    public async Task Login_With_External_ReturnUrl_Should_Return_BadRequest()
    {
        // Arrange
        const string externalReturnUrl = "https://attacker.example/callback";

        // Act
        using HttpResponseMessage response = await AppHttpClient.GetAsync(
            $"/connect/login?returnUrl={Uri.EscapeDataString(externalReturnUrl)}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task CreateIdentityUserAsync(string email, string password)
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser(
            email,
            Username.Create(email).Value,
            DisplayName.Create("OIDC Browser User").Value,
            currentCompanyId: null);

        IdentityResult result = await userManager.CreateAsync(user, password);

        result.Succeeded.Should().BeTrue(
            string.Join("; ", result.Errors.Select(error => error.Description)));
    }
}
