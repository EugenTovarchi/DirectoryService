using DirectoryService.Contracts.Requests.Positions;
using SharedService.Core.Abstractions;

namespace DirectoryService.Application.Commands.Positions.Create;

public record CreatePositionCommand(CreatePositionRequest Request) : ICommand;