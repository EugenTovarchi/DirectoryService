using CSharpFunctionalExtensions;
using DirectoryService.Application.Database;
using DirectoryService.Core.Abstractions;
using DirectoryService.SharedKernel;
using DirectoryService.SharedKernel.ValueObjects.Ids;
using Microsoft.Extensions.Logging;

namespace DirectoryService.Application.Commands.Departments.SoftDelete;

public class SoftDeleteHandler : ICommandHandler<Guid, SoftDeleteCommand>
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly ILocationRepository _locationRepository;
    private readonly IPositionRepository _positionRepository;
    private readonly ITransactionManager  _transactionManager;
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
        var transactionScopeResult = await _transactionManager.BeginTransactionAsync(cancellationToken);
        if (transactionScopeResult.IsFailure)
            return transactionScopeResult.Error.ToFailure();

        using var transactionScope = transactionScopeResult.Value;

        var lockDepartmentResult = await _departmentRepository.GetByIdWithLock(command.DepartmentId, cancellationToken);
        if (lockDepartmentResult.IsFailure)
            return Errors.General.NotFoundEntity("department").ToFailure();
        
        var department = lockDepartmentResult.Value;
        var oldPath = department.Path.Value;
        
        var lockDescendantsResult = await _departmentRepository.LockDescendantsByPath(oldPath, cancellationToken);
        if (lockDescendantsResult.IsFailure)
            return lockDescendantsResult.Error.ToFailure();
        
        department.Delete();
        
        var deletionPrefix = Constants.DELETION_PREFIX;
        
        var markedDescendentsResult = await _departmentRepository.PutDescendantsPrefixToLastPathElement(
            oldPath,
            deletionPrefix,
            department.Id,
            cancellationToken);
        if(markedDescendentsResult.IsFailure)
            return markedDescendentsResult.Error.ToFailure();
        
        var softDeleteLocationsResult = await SoftDeleteLocations(department.Id, cancellationToken);
        if (softDeleteLocationsResult.IsFailure)
        {
            transactionScope.Rollback();
            return  softDeleteLocationsResult.Error.ToFailure();
        }
        
        var softDeletePositionsResult = await SoftDeletePositions(department.Id, cancellationToken);
        if (softDeletePositionsResult.IsFailure)
        {
            transactionScope.Rollback();
            return  softDeletePositionsResult.Error.ToFailure();
        }
        
        await _transactionManager.SaveChangeAsync(cancellationToken);
        
        var commitedResult = transactionScope.Commit();
        if (commitedResult.IsFailure)
        {
            return commitedResult.Error.ToFailure();
        }

        _logger.LogInformation("Soft deleted department: {DepartmentId} and his descendants", department.Id);

        return department.Id.Value;
    }

    private async Task<UnitResult<Error>> SoftDeleteLocations(DepartmentId departmentId,
        CancellationToken cancellationToken)
    {
        var uniqDepRelatedLocations = await _locationRepository
            .GetUniqDepRelatedLocations(departmentId, cancellationToken);
        if (uniqDepRelatedLocations.IsFailure)
            return uniqDepRelatedLocations.Error;
        
        var locationsForDelete = uniqDepRelatedLocations.Value;

        foreach (var location in locationsForDelete)
        {
            location.Delete();
        }

        return Result.Success<Error>();
    }
    
    private async Task<UnitResult<Error>> SoftDeletePositions(DepartmentId departmentId,
        CancellationToken cancellationToken)
    {
        var uniqDepRelatedPositions = await _positionRepository.GetUniqDepRelatedPositions(departmentId, cancellationToken);
        if (uniqDepRelatedPositions.IsFailure)
            return uniqDepRelatedPositions.Error;
        
        var positionsForDelete = uniqDepRelatedPositions.Value;

        foreach (var position in positionsForDelete)
        {
            position.Delete();
        }

        return Result.Success<Error>();
    }
}