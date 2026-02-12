using DirectoryService.Contracts.Requests.Departments;
using SharedService.Core.Abstractions;

namespace DirectoryService.Application.Commands.Departments.UpdateDepartmentLocations;

public record UpdateDepartmentLocationsCommand(Guid DepartmentId, UpdateDepartmentLocationsRequest Request) : ICommand;

