using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace FileService.Contracts.HttpCommunication;

internal interface IServiceTokenProvider
{
    Task<Result<string, Failure>> GetAccessToken(CancellationToken cancellationToken);
}
