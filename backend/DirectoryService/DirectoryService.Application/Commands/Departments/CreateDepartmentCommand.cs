using DirectoryService.Contracts.Requests.Departments;
using DirectoryService.Core.Abstractions;

namespace DirectoryService.Application.Commands.Departments;

public record CreateDepartmentCommand(CreateDepartmentRequest Request) : ICommand;
