using System.Net.Http.Headers;
using System.Text.Json;
using DirectoryService.Application.Messaging;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharedService.SharedKernel.Messaging.Files;
using Wolverine;

namespace DirectoryService.IntegrationTests.Messaging;

public sealed class FileEventsTopologyTests : IAsyncLifetime
{
    private const int RabbitMqPort = 5672;
    private const int RabbitMqManagementPort = 15672;
    private const string RabbitMqUsername = "directory_tests";
    private const string RabbitMqPassword = "directory_tests_password";

    private static readonly string[] ExpectedDirectoryServiceBindingKeys =
    [
        FileEventsRouting.DEPARTMENT_FILE_DELETED,
        FileEventsRouting.DEPARTMENT_FILE_UPLOADED
    ];

    private readonly IContainer _rabbitMqContainer = new ContainerBuilder()
        .WithImage("rabbitmq:4.3.0-management-alpine")
        .WithEnvironment("RABBITMQ_DEFAULT_USER", RabbitMqUsername)
        .WithEnvironment("RABBITMQ_DEFAULT_PASS", RabbitMqPassword)
        .WithPortBinding(RabbitMqPort, true)
        .WithPortBinding(RabbitMqManagementPort, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(RabbitMqManagementPort))
        .Build();

    private IHost? _host;

    private Uri RabbitMqManagementUri =>
        new($"http://127.0.0.1:{_rabbitMqContainer.GetMappedPublicPort(RabbitMqManagementPort)}");

    private string RabbitMqConnectionString =>
        $"amqp://{RabbitMqUsername}:{RabbitMqPassword}@127.0.0.1:{_rabbitMqContainer.GetMappedPublicPort(RabbitMqPort)}";

    [Fact]
    public async Task DirectoryService_Should_Provision_FileEvents_RabbitMq_Topology()
    {
        using var rabbitMqClient = CreateRabbitMqManagementClient();

        var exchange = await GetJsonWithRetryAsync(
            rabbitMqClient,
            "/api/exchanges/%2f/file.events");
        var queue = await GetJsonWithRetryAsync(
            rabbitMqClient,
            "/api/queues/%2f/directory.file.events");
        var bindings = await GetJsonWithRetryAsync(
            rabbitMqClient,
            "/api/bindings/%2f/e/file.events/q/directory.file.events");

        exchange.RootElement.GetProperty("type").GetString().Should().Be("topic");
        exchange.RootElement.GetProperty("durable").GetBoolean().Should().BeTrue();

        queue.RootElement.GetProperty("durable").GetBoolean().Should().BeTrue();
        queue.RootElement.GetProperty("type").GetString().Should().Be("quorum");

        var bindingKeys = bindings.RootElement
            .EnumerateArray()
            .Select(binding => binding.GetProperty("routing_key").GetString())
            .Where(bindingKey => !string.IsNullOrWhiteSpace(bindingKey))
            .ToArray();

        bindingKeys.Should().BeEquivalentTo(ExpectedDirectoryServiceBindingKeys);
        bindingKeys.Should().NotContain(FileEventsRouting.ALL_FILE_DELETED);
        bindingKeys.Should().NotContain(FileEventsRouting.ALL_FILE_UPLOADED);
    }

    public async Task InitializeAsync()
    {
        await _rabbitMqContainer.StartAsync();

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
            })
            .UseWolverine(options =>
            {
                options.ConfigureRabbitMq(RabbitMqConnectionString);
            })
            .Build();

        await _host.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        await _rabbitMqContainer.DisposeAsync();
    }

    private HttpClient CreateRabbitMqManagementClient()
    {
        var client = new HttpClient
        {
            BaseAddress = RabbitMqManagementUri
        };
        var credentials = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes(
                $"{RabbitMqUsername}:{RabbitMqPassword}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        return client;
    }

    private static async Task<JsonDocument> GetJsonWithRetryAsync(HttpClient client, string requestUri)
    {
        var attempts = 120;
        Exception? lastException = null;

        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                using var response = await client.GetAsync(requestUri);
                if (response.IsSuccessStatusCode)
                {
                    await using var stream = await response.Content.ReadAsStreamAsync();

                    return await JsonDocument.ParseAsync(stream);
                }

                lastException = new HttpRequestException(
                    $"RabbitMQ Management API returned {(int)response.StatusCode} for {requestUri}.");
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        throw lastException ?? new InvalidOperationException($"RabbitMQ Management API did not return {requestUri}.");
    }
}
