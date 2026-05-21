using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace AuthService.Core.Abstractions;

public interface ITransactionScope : IDisposable
{
    UnitResult<Error> Commit();

    UnitResult<Error> Rollback();
}
