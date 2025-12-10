using CSharpFunctionalExtensions;
using Dapper;
using DirectoryService.Application.Database;
using DirectoryService.Core.Abstractions;
using DirectoryService.Core.Validation;
using DirectoryService.Domain.Entities;
using DirectoryService.SharedKernel;
using DirectoryService.SharedKernel.ValueObjects.Ids;
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

        await UpdateAllDescendantsPaths(oldPath, newPath, department.Id, cancellationToken);

        await _trasactionManager.SaveChangeAsync(cancellationToken);

        var commitedResult = transactionScope.Commit();
        if (commitedResult.IsFailure)
        {
            return commitedResult.Error.ToFailure();
        }

        return department.Id.Value;
    }

    private async Task UpdateAllDescendantsPaths(
            string oldPath,
            string newPath,
            DepartmentId movedDepartmentId,
            CancellationToken cancellationToken)
    {
        const string sql = """
        UPDATE departments dept
        SET 
            path = @NewPath::ltree || subpath(dept.path, nlevel(@OldPath::ltree) - 1),
            depth = nlevel(@NewPath::ltree) + 
                    (dept.depth - nlevel(@OldPath::ltree)),
            updated_at = @UpdatedAt
        WHERE dept.is_deleted = false
          AND dept.path <@ @OldPath::ltree
          AND dept.id != @MovedDepartmentId
        """;

        using var connection = await _pgsqlConnectionFactory.CreateConnectionAsync(cancellationToken);

        await connection.ExecuteAsync(sql, new
        {
            OldPath = oldPath,
            NewPath = newPath,
            MovedDepartmentId = movedDepartmentId.Value,
            UpdatedAt = DateTime.UtcNow
        });
    }
}
