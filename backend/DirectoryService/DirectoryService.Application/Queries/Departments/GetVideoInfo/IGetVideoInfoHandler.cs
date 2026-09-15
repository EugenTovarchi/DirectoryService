using CSharpFunctionalExtensions;
using DirectoryService.Contracts.Responses;
using SharedService.SharedKernel;

namespace DirectoryService.Application.Queries.Departments.GetVideoInfo;

public interface IGetVideoInfoHandler
{
    Task<Result<GetVideoInfoResponse, Failure>> Handle(
        GetVideoInfoQuery query,
        CancellationToken cancellationToken = default);
}
