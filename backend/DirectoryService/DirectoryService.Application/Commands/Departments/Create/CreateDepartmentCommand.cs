using DirectoryService.Contracts.Requests.Departments;
using SharedService.Core.Abstractions;
using IstredDev.SharedKernel.Abstractions;

namespace DirectoryService.Application.Commands.Departments.Create;

public record CreateDepartmentCommand(CreateDepartmentRequest Request) : ICommand;
