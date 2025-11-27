using CSharpFunctionalExtensions;
using DirectoryService.SharedKernel;

namespace DirectoryService.Core.Abstractions;

public interface ICommandHandler<TResponse, in TCommand>
        where TCommand : ICommand
{
    Task<Result<TResponse, Failure>> Handle(TCommand command, CancellationToken ct);
}

public interface ICommandHandler<in TCommand>
     where TCommand : ICommand
{
    Task<UnitResult<Failure>> Handle(TCommand command, CancellationToken ct);
}
