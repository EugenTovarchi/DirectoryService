using DirectoryService.Contracts.Requests.Departments;
using DirectoryService.Core.Abstractions;

namespace DirectoryService.Application.Commands.Departments.Create;

public record CreateDepartmentCommand(CreateDepartmentRequest Request) : ICommand;
