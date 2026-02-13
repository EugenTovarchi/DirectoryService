using DirectoryService.Contracts.Requests.Locations;
using SharedService.Core.Abstractions;

namespace DirectoryService.Application.Commands.Locations.Create;

public record CreateLocationCommand(CreateLocationRequest Request) : ICommand;

