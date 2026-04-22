using DirectoryService.Application.Database;
using DirectoryService.Domain.Entities;
using Microsoft.Extensions.Logging;
using SharedService.SharedKernel.Messaging.Files.Events;

namespace DirectoryService.Application.Messaging.EventHandles.Departments;

public class DepartmentFileUploadedHandler
{
    private readonly ILogger<DepartmentFileUploadedHandler> _logger;
    private readonly ITransactionManager _transactionManager;
    private readonly IDepartmentRepository _departmentRepository;

    public DepartmentFileUploadedHandler(
        ITransactionManager transactionManager,
        ILogger<DepartmentFileUploadedHandler> logger,
        IDepartmentRepository departmentRepository)
    {
        _transactionManager = transactionManager;
        _logger = logger;
        _departmentRepository = departmentRepository;
    }

    public async Task Handle(FileUploaded message, CancellationToken cancellationToken)
    {
        if (!string.Equals(message.TargetEntityType,
                MessagingConstants.ENTITY_TYPE_DEPARTMENT,
                StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Ignoring FileUploaded for {EntityType}:{EntityId}",
                message.TargetEntityType, message.TargetEntityId);
            return;
        }

        _logger.LogInformation("Received FileUploaded event for department: {departmentId}" +
                               " with video: {videoId}", message.TargetEntityId, message.AssetId);

        var departmentResult = await _departmentRepository.GetBy(d => d.Id == message.TargetEntityId, cancellationToken);
        if (departmentResult.IsFailure)
        {
            _logger.LogWarning("Department {departmentId} not found for FileUploaded event." +
                               "FileId  = {message.AssetId}", message.TargetEntityId, message.AssetId);
            return;
        }

        Department department = departmentResult.Value;

        switch (message.AssetType.ToUpperInvariant())
        {
            case MessagingConstants.ASSET_TYPE_VIDEO:
                if (department.VideoAssetId != message.AssetId)
                {
                    department.UpdateVideoId(message.AssetId);
                    await _transactionManager.SaveChangeAsync(cancellationToken);
                    _logger.LogInformation("Updated VideoAssetId for department {DepartmentId} to {AssetId}",
                                           department.Id, message.AssetId);
                }
                else
                {
                    _logger.LogInformation(
                        "VideoAssetId for department {DepartmentId} already set to {AssetId}",
                        department.Id, message.AssetId);
                }

                break;
            case MessagingConstants.ASSET_TYPE_PHOTO:
                if (department.PhotoAssetId != message.AssetId)
                {
                    department.UpdatePhotoId(message.AssetId);
                    await _transactionManager.SaveChangeAsync(cancellationToken);
                    _logger.LogInformation("Updated PhotoAssetId for department {DepartmentId} to {AssetId}",
                                           department.Id, message.AssetId);
                }
                else
                {
                    _logger.LogInformation(
                        "PhotoAssetId for department {DepartmentId} already set to {AssetId}",
                        department.Id, message.AssetId);
                }

                break;

            default:
                _logger.LogWarning(
                    "Unknown AssetType {AssetType} for file {AssetId}",
                    message.AssetType, message.AssetId);
                break;
        }
    }
}