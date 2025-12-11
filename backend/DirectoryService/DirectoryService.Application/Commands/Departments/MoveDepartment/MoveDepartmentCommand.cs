using DirectoryService.Contracts.Requests.Departments;
using DirectoryService.Core.Abstractions;

namespace DirectoryService.Application.Commands.Departments.MoveDepartment;

public record MoveDepartmentCommand(Guid DepartmentId, MoveDepartmentRequest Request): ICommand;
