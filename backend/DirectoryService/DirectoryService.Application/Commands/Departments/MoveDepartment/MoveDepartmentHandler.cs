using CSharpFunctionalExtensions;
using DirectoryService.Application.Database;
using DirectoryService.Domain.Entities;
using FluentValidation;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using SharedService.Core.Abstractions;
using SharedService.Core.Validation;
using SharedService.SharedKernel;

namespace DirectoryService.Application.Commands.Departments.MoveDepartment;

public class MoveDepartmentHandler : ICommandHandler<Guid, MoveDepartmentCommand>
{
    private readonly ITransactionManager _transactionManager;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IValidator<MoveDepartmentCommand> _validator;
    private readonly ILogger<MoveDepartmentHandler> _logger;
    private readonly HybridCache _cache;

    public MoveDepartmentHandler(
        IDepartmentRepository departmentRepository,
        IValidator<MoveDepartmentCommand> validator,
        HybridCache cache,
        ITransactionManager transactionManager,
        ILogger<MoveDepartmentHandler> logger)
    {
        _transactionManager = transactionManager;
        _cache = cache;
        _departmentRepository = departmentRepository;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<Guid, Failure>> Handle(MoveDepartmentCommand command, CancellationToken cancellationToken)
    {
        if (command == null)
            return Errors.General.ValueIsInvalid("command").ToFailure();

        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            _logger.LogWarning("Department: {command} is invalid!", command.DepartmentId);

            return validationResult.ToErrors();
        }

        var transactionScopeResult = await _transactionManager.BeginTransactionAsync(cancellationToken: cancellationToken);
        if (transactionScopeResult.IsFailure)
            return transactionScopeResult.Error.ToFailure();

        using var transactionScope = transactionScopeResult.Value;

        var isDepartmentExistResult = await _departmentRepository.GetByIdWithLock(command.DepartmentId,
            cancellationToken);
        if (isDepartmentExistResult.IsFailure)
            return Errors.General.NotFoundEntity("department").ToFailure();

        var department = isDepartmentExistResult.Value;
        string oldPath = department.Path.Value;

        Department? newParent = null;

        if (command.Request.ParentId.HasValue)
        {
            var parentResult = await _departmentRepository.GetByIdWithLock(
                command.Request.ParentId.Value, cancellationToken);

            if (parentResult.IsFailure)
                return Errors.General.NotFoundEntity("parent").ToFailure();

            newParent = parentResult.Value;

            if (command.Request.ParentId.Value == command.DepartmentId)
            {
                return Error.Conflict("department.in.conflict",
                    "Cannot move department to itself").ToFailure();
            }
        }

        var lockResult = await _departmentRepository.LockDescendantsByPath(oldPath, cancellationToken);
        if (lockResult.IsFailure)
            return lockResult.Error.ToFailure();

        var updateDepartmentResult = department.MoveTo(newParent);
        if(updateDepartmentResult.IsFailure)
            return updateDepartmentResult.Error.ToFailure();

        string newPath = department.Path.Value;

        var updateDescendantsResult = await _departmentRepository.UpdateAllDescendantsPath(oldPath, newPath, department.Id,
            cancellationToken);
        if (updateDescendantsResult.IsFailure)
            return updateDescendantsResult.Error.ToFailure();

        await _transactionManager.SaveChangeAsync(cancellationToken);

        var commitedResult = transactionScope.Commit();
        if (commitedResult.IsFailure)
        {
            return commitedResult.Error.ToFailure();
        }

        await _cache.RemoveByTagAsync("departments", cancellationToken);
        _logger.LogInformation("Cache with tag: 'departments' was cleaned");

        return department.Id.Value;
    }
}
