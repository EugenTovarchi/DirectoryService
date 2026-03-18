using CSharpFunctionalExtensions;
using FileService.Contracts.Requests;
using FileService.Contracts.Responses;
using Microsoft.Extensions.Logging;
using SharedService.Framework.ControllersResults;
using SharedService.SharedKernel;

namespace FileService.Contracts.HttpCommunication;

internal sealed class FileHttpClient : IFileCommunicationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FileHttpClient> _logger;

    public FileHttpClient(HttpClient httpClient, ILogger<FileHttpClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<Result<GetMediaAssetResponse, Failure>> GetMediaAssetInfo(Guid mediaAssetId,
        CancellationToken cancellationToken)
    {
        try
        {
            HttpResponseMessage response =
                await _httpClient.GetAsync("api/files/{mediaAssetId:guid}", cancellationToken);

            return await response.HandleResponseAsync<GetMediaAssetResponse>(cancellationToken);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting media asset info for {mediaAssetId}", mediaAssetId);
            return Error.Failure("server.internal", "Failed to request media asset info").ToFailure();
        }
    }

    public async Task<Result<GetMediaAssetsResponse, Failure>> GetMediaAssetsInfo(GetMediaAssetsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            HttpResponseMessage response =
                await _httpClient.GetAsync("api/files/batch", cancellationToken);

            return await response.HandleResponseAsync<GetMediaAssetsResponse>(cancellationToken);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting media asset info for {mediaAssetIds}", request.MediaAssetIds);
            return Error.Failure("server.internal", "Failed to request media asset info").ToFailure();
        }
    }
}