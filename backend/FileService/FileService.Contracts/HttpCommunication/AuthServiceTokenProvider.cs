using System.Net.Http.Json;
using System.Text.Json.Serialization;
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

            var tokenForm = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _options.ServiceClientId,
                ["client_secret"] = _options.ServiceClientSecret,
                ["scope"] = _options.ServiceTokenScope
            };

            HttpResponseMessage response = await _httpClient
                .PostAsync(
                    "connect/token",
                    new FormUrlEncodedContent(tokenForm),
                    cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("AuthService service-token request failed with {StatusCode}", response.StatusCode);
                return Error.Failure("auth.service_token.failed", "Failed to request service access token").ToFailure();
            }

            OAuthTokenResponse? tokenResponse = await response.Content
                .ReadFromJsonAsync<OAuthTokenResponse>(cancellationToken)
                .ConfigureAwait(false);

            if (tokenResponse is null ||
                string.IsNullOrWhiteSpace(tokenResponse.AccessToken) ||
                tokenResponse.ExpiresIn <= 0)
            {
                return Error.Failure("auth.service_token.invalid_response", "Service token response is invalid")
                    .ToFailure();
            }

            _accessToken = tokenResponse.AccessToken;
            _accessTokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

            return _accessToken;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
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

    private sealed record OAuthTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    public void Dispose()
    {
        _tokenLock.Dispose();
    }
}
