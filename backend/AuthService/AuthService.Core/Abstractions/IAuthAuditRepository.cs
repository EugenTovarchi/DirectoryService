using AuthService.Domain.Identity;
using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace AuthService.Core.Abstractions;

public interface IAuthAuditRepository
{
    UnitResult<Error> Add(AuthAuditEvent auditEvent);
}
