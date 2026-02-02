using CSharpFunctionalExtensions;
using DirectoryService.Application.Database;
using DirectoryService.Core.Abstractions;
using DirectoryService.SharedKernel;
using Microsoft.Extensions.Logging;

namespace DirectoryService.Application.Commands.Departments.SoftDelete;

public class SoftDeleteHandler : ICommandHandler<Guid, SoftDeleteCommand>
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly ILocationRepository _locationRepository;
    private readonly IPositionRepository _positionRepository;
    private readonly ITransactionManager _transactionManager;
    private readonly ILogger<SoftDeleteHandler> _logger;

    public SoftDeleteHandler(IDepartmentRepository repository,
        ITransactionManager transactionManager,
        IPositionRepository positionRepository,
        ILocationRepository locationRepository,
        ILogger<SoftDeleteHandler> logger)
    {
        _departmentRepository = repository;
        _transactionManager = transactionManager;
        _logger = logger;
        _positionRepository = positionRepository;
        _locationRepository = locationRepository;
    }

    public async Task<Result<Guid, Failure>> Handle(SoftDeleteCommand command, CancellationToken cancellationToken)
    {
        var transactionScopeResult = await _transactionManager.BeginTransactionAsync(cancellationToken: cancellationToken);
        if (transactionScopeResult.IsFailure)
            return transactionScopeResult.Error.ToFailure();

        using var transactionScope = transactionScopeResult.Value;

        var lockDepartmentResult = await _departmentRepository.GetByIdWithLock(command.DepartmentId, cancellationToken);
        if (lockDepartmentResult.IsFailure)
        {
            transactionScope.Rollback();
            return Errors.General.NotFoundEntity("department").ToFailure();
        }

        var department = lockDepartmentResult.Value;
        var oldPath = department.Path.Value;

        var lockDescendantsResult = await _departmentRepository.LockDescendantsByPath(oldPath, cancellationToken);
        if (lockDescendantsResult.IsFailure)
        {
            transactionScope.Rollback();
            return lockDescendantsResult.Error.ToFailure();
        }

        department.Delete();

        var deletionPrefix = Constants.DELETION_PREFIX;

        var updateDepPathResult = await _departmentRepository
            .MarkDepartmentAsDeleted(deletionPrefix, department.Id, cancellationToken);
        if (updateDepPathResult.IsFailure)
        {
            _logger.LogError("Error when update path of department:{department}.", department.Id);
            transactionScope.Rollback();
            return updateDepPathResult.Error.ToFailure();
        }

        var newPath = department.Path.Value;

        var updateDescendantsPathResult = await _departmentRepository.UpdateAllDescendantsPath(
            oldPath,
            newPath,
            department.Id,
            cancellationToken);
        if (updateDescendantsPathResult.IsFailure)
        {
            _logger.LogError("Error when update path descendants of department:{department}", department.Id);
            transactionScope.Rollback();
            return updateDescendantsPathResult.Error.ToFailure();
        }

        var updatedPositionsResult = await _positionRepository.SoftDeleteUniqDepRelatedPositions(department.Id,
            cancellationToken);
        if (updatedPositionsResult.IsFailure)
        {
            transactionScope.Rollback();
            return updatedPositionsResult.Error.ToFailure();
        }

        var updatedLocationsResult = await _locationRepository.SoftDeleteUniqDepRelatedLocations(department.Id,
            cancellationToken);
        if (updatedLocationsResult.IsFailure)
        {
            transactionScope.Rollback();
        }

        await _transactionManager.SaveChangeAsync(cancellationToken);

        var commitedResult = transactionScope.Commit();
        if (commitedResult.IsFailure)
        {
            return commitedResult.Error.ToFailure();
        }

        _logger.LogInformation("Department: {DepartmentId} was soft deleted with descendants.", department.Id);

        return department.Id.Value;
    }
}