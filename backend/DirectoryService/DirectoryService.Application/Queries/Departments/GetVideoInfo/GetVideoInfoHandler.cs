using CSharpFunctionalExtensions;
using DirectoryService.Application.Database;
using DirectoryService.Contracts.Responses;
using FileService.Contracts.HttpCommunication;
using SharedService.Core.Abstractions;
using SharedService.SharedKernel;

namespace DirectoryService.Application.Queries.Departments.GetVideoInfo;

public class GetVideoInfoHandler
    : IQueryHandler<Result<GetVideoInfoResponse, Failure>, GetVideoInfoQuery>,
      IGetVideoInfoHandler
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IFileCommunicationService _fileCommunicationService;

    public GetVideoInfoHandler(
        IDepartmentRepository departmentRepository,
        IFileCommunicationService fileCommunicationService)
    {
        _departmentRepository = departmentRepository;
        _fileCommunicationService = fileCommunicationService;
    }

    public async Task<Result<GetVideoInfoResponse, Failure>> Handle(
        GetVideoInfoQuery query,
        CancellationToken ct = default)
    {
        var departmentResult =
            await _departmentRepository.GetById(query.DepartmentId, ct);

        if (departmentResult.IsFailure)
        {
            return Errors.General
                .NotFoundEntity("department")
                .ToFailure();
        }

        var department = departmentResult.Value;

        if (department.VideoAssetId is null)
        {
            return Errors.General
                .NotFoundEntity("video")
                .ToFailure();
        }

        Guid videoId = department.VideoAssetId.Value;

        var videoInfoResponse = await _fileCommunicationService.GetVideoInfo(
            videoId, ct);

        if (videoInfoResponse.IsFailure)
            return videoInfoResponse.Error;

        var response =
            new GetVideoInfoResponse(
                videoInfoResponse.Value.Id,
                videoInfoResponse.Value.FileName,
                videoInfoResponse.Value.ContentType,
                videoInfoResponse.Value.Status,
                videoInfoResponse.Value.CreatedAt,
                videoInfoResponse.Value.UpdatedAt,
                videoInfoResponse.Value.Size,
                videoInfoResponse.Value.Duration,
                videoInfoResponse.Value.Width,
                videoInfoResponse.Value.Height,
                videoInfoResponse.Value.HasAudio);

        return response;
    }
}
