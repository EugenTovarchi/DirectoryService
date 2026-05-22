using AuthService.Core.Abstractions;
using AuthService.Domain.Identity;
using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace AuthService.Infrastructure.Postgres.Repositories;

public sealed class AuthAuditRepository : IAuthAuditRepository
{
    private readonly AuthServiceDbContext _dbContext;

    public AuthAuditRepository(AuthServiceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public UnitResult<Error> Add(AuthAuditEvent auditEvent)
    {
        if (auditEvent is null)
            return Errors.General.ValueIsInvalid("auditEvent");

        _dbContext.AuthAuditEvents.Add(auditEvent);

        return UnitResult.Success<Error>();
    }
}
