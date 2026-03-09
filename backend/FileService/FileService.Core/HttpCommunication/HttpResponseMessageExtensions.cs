using System.Net.Http.Json;
using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace FileService.Core.HttpCommunication;

public static class HttpResponseMessageExtensions
{
    public static async Task<Result<TResponse, Failure>> HandleResponseAsync<TResponse>(
        this HttpResponseMessage response, CancellationToken cancellationToken = default)
        where TResponse : class
    {
        try
        {
            Envelope<TResponse>? startMultipartUploadResponse = await response.Content
                .ReadFromJsonAsync<Envelope<TResponse>>(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return startMultipartUploadResponse?.Errors ?? Error.Failure("test.error", "Unknown error");
            }

            if (startMultipartUploadResponse is null)
            {
                return Error.Failure("http_response.error", "Error while reading response!").ToFailure();
            }

            if (startMultipartUploadResponse.Result is null)
            {
                return Error.Failure("http_response.error", "Error while reading response!").ToFailure();
            }

            if (startMultipartUploadResponse.Errors is not null)
            {
                return startMultipartUploadResponse.Errors;
            }

            return (Result<TResponse, Failure>)startMultipartUploadResponse.Result;
        }
        catch
        {
            return Error.Failure("http_response.error", "Error while reading response!").ToFailure();
        }
    }

    public static async Task<UnitResult<Failure>> HandleResponse(
        this HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        try
        {
            Envelope? startMultipartUploadResponse = await response.Content
                .ReadFromJsonAsync<Envelope>(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return startMultipartUploadResponse?.Errors ?? Error.Failure("test.error", "Unknown error");
            }

            if (startMultipartUploadResponse is null)
            {
                return Error.Failure("http_response.error", "Error while reading response!").ToFailure();
            }

            if (startMultipartUploadResponse.Errors is not null)
            {
                return startMultipartUploadResponse.Errors;
            }

            return UnitResult.Success<Failure>();
        }
        catch
        {
            return Error.Failure("http_response.error", "Error while reading response!").ToFailure();
        }
    }
}