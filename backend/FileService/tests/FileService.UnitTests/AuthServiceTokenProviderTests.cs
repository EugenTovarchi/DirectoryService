using System.Net;
using FileService.Contracts.HttpCommunication;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FileService.UnitTests;

public sealed class AuthServiceTokenProviderTests
{
    [Fact]
    public async Task GetAccessToken_With_Valid_Response_Should_Use_ClientCredentials_And_Cache_Token()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler(
            """
            {
              "access_token": "service-access-token",
              "token_type": "Bearer",
              "expires_in": 900
            }
            """);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://auth-service.test/")
        };
        FileServiceOptions options = new()
        {
            ServiceClientId = "directory-service",
            ServiceClientSecret = "test-directory-service-client-secret",
            ServiceTokenScope = "files"
        };
        using var provider = new AuthServiceTokenProvider(
            httpClient,
            Options.Create(options),
            NullLogger<AuthServiceTokenProvider>.Instance);

        // Act
        var firstResult = await provider.GetAccessToken(CancellationToken.None);
        var secondResult = await provider.GetAccessToken(CancellationToken.None);

        // Assert
        firstResult.IsSuccess.Should().BeTrue();
        firstResult.Value.Should().Be("service-access-token");
        secondResult.IsSuccess.Should().BeTrue();
        secondResult.Value.Should().Be("service-access-token");
        handler.RequestCount.Should().Be(1);
        handler.RequestMethod.Should().Be(HttpMethod.Post);
        handler.RequestUri.Should().Be(new Uri("https://auth-service.test/connect/token"));
        handler.RequestContentType.Should().Be("application/x-www-form-urlencoded");
        handler.RequestBody.Should().Contain("grant_type=client_credentials");
        handler.RequestBody.Should().Contain("client_id=directory-service");
        handler.RequestBody.Should().Contain("client_secret=test-directory-service-client-secret");
        handler.RequestBody.Should().Contain("scope=files");
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _responseBody;

        public RecordingHttpMessageHandler(string responseBody)
        {
            _responseBody = responseBody;
        }

        public int RequestCount { get; private set; }
        public HttpMethod? RequestMethod { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? RequestContentType { get; private set; }
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            RequestMethod = request.Method;
            RequestUri = request.RequestUri;
            RequestContentType = request.Content?.Headers.ContentType?.MediaType;
            RequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    _responseBody,
                    System.Text.Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
