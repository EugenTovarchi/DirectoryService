using System.Net;
using System.Text.RegularExpressions;
using AuthService.Domain.Identity;
using AuthService.IntegrationTests.Infrastructure;
using AuthService.Web.Configurations;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
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

    [Fact]
    public async Task Login_With_Invalid_Password_Should_Return_Indistinguishable_Failure()
    {
        // Arrange
        const string email = "oidc-invalid-password@24eye.test";
        await CreateIdentityUserAsync(email, "password123");

        // Act
        OidcTestToken? token = await TryLoginWithOidcAsync(email, "wrong-password");

        // Assert
        token.Should().BeNull();
    }

    [Fact]
    public async Task Login_After_Three_Invalid_Passwords_Should_Temporarily_Lock_User()
    {
        // Arrange
        const string email = "oidc-lockout@24eye.test";
        ApplicationUser user = await CreateIdentityUserAsync(email, "password123");
        var invalidTokens = new List<OidcTestToken?>();

        // Act
        for (int attempt = 0; attempt < 3; attempt++)
        {
            invalidTokens.Add(await TryLoginWithOidcAsync(email, "wrong-password"));
        }

        OidcTestToken? validPasswordToken =
            await TryLoginWithOidcAsync(email, "password123");
        ApplicationUser lockedUser = await ExecuteInDb(dbContext => dbContext.Users
            .SingleAsync(identityUser => identityUser.Id == user.Id));

        // Assert
        invalidTokens.Should().OnlyContain(token => token == null);
        validPasswordToken.Should().BeNull();
        lockedUser.LockoutEnd.Should().NotBeNull();
        lockedUser.LockoutEnd.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Login_With_Inactive_User_Should_Return_Indistinguishable_Failure()
    {
        // Arrange
        const string email = "oidc-inactive@24eye.test";
        ApplicationUser user = await CreateIdentityUserAsync(email, "password123");
        await ExecuteInDb(async dbContext =>
        {
            ApplicationUser trackedUser = await dbContext.Users
                .SingleAsync(identityUser => identityUser.Id == user.Id);
            trackedUser.Deactivate();
            await dbContext.SaveChangesAsync();
            return true;
        });

        // Act
        OidcTestToken? token = await TryLoginWithOidcAsync(email, "password123");

        // Assert
        token.Should().BeNull();
    }

    [Fact]
    public async Task Login_When_Rate_Limit_Is_Exceeded_Should_Return_TooManyRequests()
    {
        // Arrange
        using HttpClient rateLimitedClient = _factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.PostConfigure<PublicAuthRateLimitOptions>(options =>
                    {
                        options.WindowSeconds = 60;
                        options.LoginPermitLimit = 1;
                    });
                });
            })
            .CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    AllowAutoRedirect = false,
                    BaseAddress = new Uri("https://auth-service.tests")
                });

        // Act
        using HttpResponseMessage firstResponse = await SubmitLoginAsync(
            rateLimitedClient,
            "rate-limit@example.com",
            "wrong-password");
        using HttpResponseMessage secondResponse = await SubmitLoginAsync(
            rateLimitedClient,
            "rate-limit@example.com",
            "wrong-password");

        // Assert
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    private async Task<ApplicationUser> CreateIdentityUserAsync(string email, string password)
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

        return user;
    }

    /// <summary>
    /// Отправляет browser login form с корректным antiforgery token.
    /// </summary>
    private static async Task<HttpResponseMessage> SubmitLoginAsync(
        HttpClient client,
        string email,
        string password)
    {
        const string returnUrl = "/connect/authorize?client_id=oidc-public-tests";

        using HttpResponseMessage loginPageResponse = await client.GetAsync(
            $"/connect/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        string loginPage = await loginPageResponse.Content.ReadAsStringAsync();
        Match tokenMatch = Regex.Match(
            loginPage,
            "name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"",
            RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
            TimeSpan.FromSeconds(1));
        if (!tokenMatch.Success)
        {
            throw new InvalidOperationException("Antiforgery token was not rendered");
        }

        var form = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Email"] = email,
            ["Password"] = password,
            ["ReturnUrl"] = returnUrl,
            ["__RequestVerificationToken"] = tokenMatch.Groups["token"].Value
        };

        return await client.PostAsync(
            "/connect/login",
            new FormUrlEncodedContent(form));
    }
}
