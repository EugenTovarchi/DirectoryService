using DirectoryService.Core.Abstractions;

namespace DirectoryService.Application.Commands.Departments.SoftDelete;

public record SoftDeleteCommand(Guid DepartmentId) : ICommand;
