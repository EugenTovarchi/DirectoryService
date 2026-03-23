using DirectoryService.Contracts.Requests.Departments;
using SharedService.Core.Abstractions;

namespace DirectoryService.Application.Commands.Departments.UpdateVideo;

public record UpdateVideoCommand(Guid DepartmentId, UpdateVideoRequest Request) : ICommand;
