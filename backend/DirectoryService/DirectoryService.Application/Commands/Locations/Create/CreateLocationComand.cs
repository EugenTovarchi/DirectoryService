using DirectoryService.Contracts.Requests.Locations;
using DirectoryService.Core.Abstractions;

namespace DirectoryService.Application.Commands.Locations.Create;

public  record CreateLocationCommand(CreateLocationRequest Request) : ICommand;

