using System.Net.Http.Json;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedService.SharedKernel;

namespace FileService.Contracts.HttpCommunication;

internal sealed class AuthServiceTokenProvider : IServiceTokenProvider, IDisposable
{
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromSeconds(30);

    private readonly HttpClient _httpClient;
    private readonly FileServiceOptions _options;
    private readonly ILogger<AuthServiceTokenProvider> _logger;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    private string? _accessToken;
    private DateTime _accessTokenExpiresAt;

    public AuthServiceTokenProvider(
        HttpClient httpClient,
        IOptions<FileServiceOptions> options,
        ILogger<AuthServiceTokenProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<string, Failure>> GetAccessToken(CancellationToken cancellationToken)
    {
        if (TokenIsUsable())
            return _accessToken!;

        await _tokenLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (TokenIsUsable())
                return _accessToken!;

            var request = new ServiceTokenRequest(
                _options.ServiceClientId,
                _options.ServiceClientSecret);

            HttpResponseMessage response = await _httpClient
                .PostAsJsonAsync("api/auth/service-token", request, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("AuthService service-token request failed with {StatusCode}", response.StatusCode);
                return Error.Failure("auth.service_token.failed", "Failed to request service access token").ToFailure();
            }

            ServiceTokenResponse? tokenResponse = await response.Content
                .ReadFromJsonAsync<ServiceTokenResponse>(cancellationToken)
                .ConfigureAwait(false);

            if (tokenResponse is null || string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
            {
                return Error.Failure("auth.service_token.invalid_response", "Service token response is invalid")
                    .ToFailure();
            }

            _accessToken = tokenResponse.AccessToken;
            _accessTokenExpiresAt = tokenResponse.AccessTokenExpiresAt;

            return _accessToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting service access token");
            return Error.Failure("auth.service_token.failed", "Failed to request service access token").ToFailure();
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private bool TokenIsUsable() =>
        !string.IsNullOrWhiteSpace(_accessToken) &&
        _accessTokenExpiresAt > DateTime.UtcNow.Add(RefreshSkew);

    private sealed record ServiceTokenRequest(string ClientId, string ClientSecret);

    private sealed record ServiceTokenResponse(string AccessToken, DateTime AccessTokenExpiresAt);

    public void Dispose()
    {
        _tokenLock.Dispose();
    }
}
