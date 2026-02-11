using CSharpFunctionalExtensions;
using DirectoryService.Application.Database;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using SharedService.Core.Abstractions;
using SharedService.SharedKernel;

namespace DirectoryService.Application.Commands.Departments.SoftDelete;

public class SoftDeleteHandler : ICommandHandler<Guid, SoftDeleteCommand>
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly ILocationRepository _locationRepository;
    private readonly IPositionRepository _positionRepository;
    private readonly ITransactionManager _transactionManager;
    private readonly HybridCache _cache;
    private readonly ILogger<SoftDeleteHandler> _logger;

    public SoftDeleteHandler(IDepartmentRepository repository,
        ITransactionManager transactionManager,
        IPositionRepository positionRepository,
        ILocationRepository locationRepository,
        HybridCache cache,
        ILogger<SoftDeleteHandler> logger)
    {
        _departmentRepository = repository;
        _transactionManager = transactionManager;
        _cache = cache;
        _positionRepository = positionRepository;
        _locationRepository = locationRepository;
        _logger = logger;
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
        string oldPath = department.Path.Value;

        var lockDescendantsResult = await _departmentRepository.LockDescendantsByPath(oldPath, cancellationToken);
        if (lockDescendantsResult.IsFailure)
        {
            transactionScope.Rollback();
            return lockDescendantsResult.Error.ToFailure();
        }

        department.Delete();

        string deletionPrefix = Constants.DELETION_PREFIX;

        var updateDepPathResult = await _departmentRepository
            .MarkDepartmentAsDeleted(deletionPrefix, department.Id, cancellationToken);
        if (updateDepPathResult.IsFailure)
        {
            _logger.LogError("Error when update path of department:{department}.", department.Id);
            transactionScope.Rollback();
            return updateDepPathResult.Error.ToFailure();
        }

        string newPath = department.Path.Value;

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

        await _cache.RemoveByTagAsync("departments", cancellationToken);
        _logger.LogInformation("Cache with tag: 'departments' was cleaned");

        return department.Id.Value;
    }
}