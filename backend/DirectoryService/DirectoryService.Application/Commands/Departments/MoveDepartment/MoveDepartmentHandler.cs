using CSharpFunctionalExtensions;
using DirectoryService.Application.Database;
using DirectoryService.Core.Abstractions;
using DirectoryService.Core.Validation;
using DirectoryService.Domain.Entities;
using DirectoryService.SharedKernel;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace DirectoryService.Application.Commands.Departments.MoveDepartment;

public class MoveDepartmentHandler : ICommandHandler<Guid, MoveDepartmentCommand>
{
    private readonly ITrasactionManager _trasactionManager;
    private readonly INpgsqlConnectionFactory _pgsqlConnectionFactory;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IValidator<MoveDepartmentCommand> _validator;
    private readonly ILogger<MoveDepartmentHandler> _logger;
    public MoveDepartmentHandler(
        IDepartmentRepository departmentRepository,
        IValidator<MoveDepartmentCommand> validator,
        ILogger<MoveDepartmentHandler> logger,
        ITrasactionManager trasactionManager,
        INpgsqlConnectionFactory pgsqlConnectionFactory)
    {
        _trasactionManager = trasactionManager;
        _departmentRepository = departmentRepository;
        _validator = validator;
        _logger = logger;
        _pgsqlConnectionFactory = pgsqlConnectionFactory;
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

        var transactionScopeResult = await _trasactionManager.BeginTransactionAsync(cancellationToken);
        if (transactionScopeResult.IsFailure)
            return transactionScopeResult.Error.ToFailure();

        using var transactionScope = transactionScopeResult.Value;

        var isDepartmentExistResult = await _departmentRepository.GetByIdWithLock(command.DepartmentId, cancellationToken);
        if (isDepartmentExistResult.IsFailure)
            return Errors.General.NotFoundEntity("department").ToFailure();

        var department = isDepartmentExistResult.Value;
        var oldPath = department.Path.Value;

        Department? newParent = null;

        if (command.Request.ParentId.HasValue)
        {
            var parentResult = await _departmentRepository.GetByIdWithLock(
                command.Request.ParentId.Value, cancellationToken);

            if (parentResult.IsFailure)
                return Errors.General.NotFoundEntity("parent").ToFailure();

            newParent = parentResult.Value;

            if (newParent.Path.Value.StartsWith(oldPath + "."))
                return Error.Conflict("department.in.conflict", "Child of department cannot be as parent").ToFailure();
        }

        var lockResult = await _departmentRepository.LockDescendantsByPath(oldPath, cancellationToken);
        if (lockResult.IsFailure)
            return lockResult.Error.ToFailure();

        var updeteDepartmentResult = department.MoveTo(newParent);
        if(updeteDepartmentResult.IsFailure)
            return updeteDepartmentResult.Error.ToFailure();

        var newPath = department.Path.Value;

        var updateDescendantsResult = await _departmentRepository.UpdateAllDescendants(oldPath, newPath, department.Id, cancellationToken);
        if (updateDescendantsResult.IsFailure)
            return updateDescendantsResult.Error.ToFailure();

        await _trasactionManager.SaveChangeAsync(cancellationToken);

        var commitedResult = transactionScope.Commit();
        if (commitedResult.IsFailure)
        {
            return commitedResult.Error.ToFailure();
        }

        return department.Id.Value;
    }
}
