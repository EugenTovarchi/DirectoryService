using AuthService.Infrastructure.Postgres;
using Microsoft.Extensions.DependencyInjection;

namespace AuthService.IntegrationTests.Infrastructure;

[Collection("AuthServiceCollection")]
public abstract class AuthServiceBaseTests : IAsyncLifetime
{
    private readonly AuthServiceTestWebFactory _factory;
    private readonly Func<Task> _resetDatabase;

    protected AuthServiceBaseTests(AuthServiceTestWebFactory factory)
    {
        _factory = factory;
        AppHttpClient = factory.CreateClient();
        Services = factory.Services;
        _resetDatabase = factory.ResetDatabaseAsync;
    }

    protected HttpClient AppHttpClient { get; }
    protected IServiceProvider Services { get; }

    /// <summary>
    /// Получает OpenIddict access token через реальный Authorization Code + PKCE flow.
    /// </summary>
    protected async Task<OidcTestToken> LoginWithOidcAsync(
        string email,
        string password = "password123",
        string? userAgent = null)
    {
        OidcTestToken? token = await TryLoginWithOidcAsync(email, password, userAgent);
        return token ?? throw new InvalidOperationException("OIDC login was rejected");
    }

    /// <summary>
    /// Возвращает null, когда OIDC browser login отклоняет credentials.
    /// </summary>
    protected Task<OidcTestToken?> TryLoginWithOidcAsync(
        string email,
        string password,
        string? userAgent = null)
    {
        return OidcAuthorizationTestHelper.TryLoginAsync(
            _factory,
            Services,
            email,
            password,
            userAgent);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => _resetDatabase();

    protected async Task<T> ExecuteInDb<T>(Func<AuthServiceDbContext, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AuthServiceDbContext>();

        return await action(dbContext);
    }
}
