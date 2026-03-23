using CSharpFunctionalExtensions;
using DirectoryService.Application.Database;
using FileService.Contracts.HttpCommunication;
using Microsoft.Extensions.Logging;
using SharedService.Core.Abstractions;
using SharedService.SharedKernel;

namespace DirectoryService.Application.Commands.Departments.UpdateVideo;

public class UpdateVideoHandler : ICommandHandler<Guid, UpdateVideoCommand>
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly ITransactionManager _transactionManager;
    private readonly IFileCommunicationService _fileCommunicationService;
    private readonly ILogger<UpdateVideoHandler> _logger;

    public UpdateVideoHandler(
        IDepartmentRepository departmentRepository,
        ITransactionManager transactionManager,
        IFileCommunicationService fileCommunicationService,
        ILogger<UpdateVideoHandler> logger)
    {
        _departmentRepository = departmentRepository;
        _transactionManager = transactionManager;
        _fileCommunicationService = fileCommunicationService;
        _logger = logger;
    }

    public async Task<Result<Guid, Failure>> Handle(UpdateVideoCommand command, CancellationToken cancellationToken)
    {
        if (command.Request.VideoId.HasValue)
        {
            var existResult = await _fileCommunicationService.CheckMediaAssetExists(command.Request.VideoId.Value,
                cancellationToken);

            if (existResult.IsFailure)
                return existResult.Error;

            if (!existResult.Value.IsExist)
                return Errors.General.NotFoundEntity("video").ToFailure();
        }

        var isDepartmentExistResult = await _departmentRepository.GetByIdWithLock(command.DepartmentId,
            cancellationToken);
        if (isDepartmentExistResult.IsFailure)
            return Errors.General.NotFoundEntity("department").ToFailure();

        var department = isDepartmentExistResult.Value;

        department.UpdateVideoId(command.Request.VideoId);

        await _transactionManager.SaveChangeAsync(cancellationToken);

        _logger.LogInformation("Video id {video_id} was updated for department:{departmentId}",
            command.Request.VideoId, department.Id);

        return department.Id.Value;
    }
}