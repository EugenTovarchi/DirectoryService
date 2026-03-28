using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace FileService.Core;

public interface ITransactionScope : IDisposable
{
    UnitResult<Error> Commit();
    UnitResult<Error> Rollback();
}