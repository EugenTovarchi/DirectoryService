    using System.Net.Http.Json;
    using CSharpFunctionalExtensions;
    using SharedService.SharedKernel;

    namespace FileService.Core.HttpCommunication;

    public static class HttpResponseMessageExtensions
    {
        public static async Task<Result<TResponse, Failure>> HandleResponseAsync<TResponse>(
            this HttpResponseMessage response, CancellationToken cancellationToken = default)
        {
            try
            {
                Envelope<TResponse>? envelope = await response.Content
                    .ReadFromJsonAsync<Envelope<TResponse>>(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return envelope?.Errors ?? Error.Failure("test.error", "Unknown error");
                }

                if (envelope is null)
                {
                    return Error.Failure("http_response.error", "Error while reading response!").ToFailure();
                }

                if (envelope.Result is null)
                {
                    return Error.Failure("http_response.error", "Error while reading response!").ToFailure();
                }

                if (envelope.Errors is not null)
                {
                    return envelope.Errors;
                }

                return envelope.Result;
            }
            catch
            {
                return Error.Failure("http_response.error", "Error while reading response!").ToFailure();
            }
        }

        /// <summary>
        /// Возможность получить null результата.
        /// </summary>
        /// <param name="response">HTTP-ответ от API, полученный после отправки запроса.</param>
        /// <param name="cancellationToken">Токен для отмены операции.</param>
        /// <typeparam name="TResponse">Тип возвращаемого результата.</typeparam>
        /// <returns>Или record или null для get запросов.</returns>
        public static async Task<Result<TResponse?, Failure>> HandleNullableResponseAsync<TResponse>(
            this HttpResponseMessage response, CancellationToken cancellationToken = default)
        {
            try
            {
                Envelope<TResponse>? envelope = await response.Content
                    .ReadFromJsonAsync<Envelope<TResponse>>(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return envelope?.Errors ?? Error.Failure("test.error", "Unknown error");
                }

                if (envelope is null)
                {
                    return Error.Failure("http_response.error", "Error while reading response!").ToFailure();
                }

                return envelope.Result;
            }
            catch
            {
                return Error.Failure("http_response.error", "Error while reading response!").ToFailure();
            }
        }

        public static async Task<UnitResult<Failure>> HandleResponseAsync(
            this HttpResponseMessage response, CancellationToken cancellationToken = default)
        {
            try
            {
                Envelope? envelope = await response.Content
                    .ReadFromJsonAsync<Envelope>(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return envelope?.Errors ?? Error.Failure("test.error", "Unknown error");
                }

                if (envelope is null)
                {
                    return Error.Failure("http_response.error", "Error while reading response!").ToFailure();
                }

                if (envelope.Errors is not null)
                {
                    return envelope.Errors;
                }

                return UnitResult.Success<Failure>();
            }
            catch
            {
                return Error.Failure("http_response.error", "Error while reading response!").ToFailure();
            }
        }
    }