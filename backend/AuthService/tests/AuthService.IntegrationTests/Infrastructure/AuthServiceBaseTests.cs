using AuthService.Infrastructure.Postgres;
using Microsoft.Extensions.DependencyInjection;

namespace AuthService.IntegrationTests.Infrastructure;

[Collection("AuthServiceCollection")]
public abstract class AuthServiceBaseTests : IClassFixture<AuthServiceTestWebFactory>, IAsyncLifetime
{
    private readonly Func<Task> _resetDatabase;

    protected AuthServiceBaseTests(AuthServiceTestWebFactory factory)
    {
        AppHttpClient = factory.CreateClient();
        Services = factory.Services;
        _resetDatabase = factory.ResetDatabaseAsync;
    }

    protected HttpClient AppHttpClient { get; }
    protected IServiceProvider Services { get; }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => _resetDatabase();

    protected async Task<T> ExecuteInDb<T>(Func<AuthServiceDbContext, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AuthServiceDbContext>();

        return await action(dbContext);
    }
}
