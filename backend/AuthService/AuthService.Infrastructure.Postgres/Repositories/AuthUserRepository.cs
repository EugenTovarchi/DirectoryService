using System.Linq.Expressions;
using AuthService.Core.Abstractions;
using AuthService.Domain.Users;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using SharedService.SharedKernel;

namespace AuthService.Infrastructure.Postgres.Repositories;

public class AuthUserRepository(
    AuthServiceDbContext dbContext,
    ILogger<AuthUserRepository> logger)
    : IAuthUserRepository
{
    public Result<Guid, Error> Add(AuthUser user)
    {
        try
        {
            dbContext.AuthUsers.Add(user);

            return user.Id;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx)
        {
            return HandlePostgresException(pgEx, user.Id);
        }
        catch (OperationCanceledException ex)
        {
            logger.LogError(ex, "Operation was cancelled while creating auth user {UserId}", user.Id);
            return Errors.General.DatabaseError("creating_auth_user_error");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while creating auth user {UserId}", user.Id);
            return Errors.General.DatabaseError("creating_auth_user_error");
        }
    }

    public async Task<Result<AuthUser, Error>> GetBy(
        Expression<Func<AuthUser, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.AuthUsers
            .AsTracking()
            .FirstOrDefaultAsync(predicate, cancellationToken);

        if (user is null)
            return Errors.General.NotFoundEntity("authUser");

        return user;
    }

    private Result<Guid, Error> HandlePostgresException(PostgresException pgEx, Guid userId)
    {
        if (!string.Equals(pgEx.SqlState, PostgresErrorCodes.UniqueViolation, StringComparison.OrdinalIgnoreCase)
            || pgEx.ConstraintName is null)
        {
            logger.LogError("Database error while creating auth user {UserId}: {Message}", userId, pgEx.MessageText);
            return Errors.General.DatabaseError("creating_auth_user_error");
        }

        logger.LogWarning(
            "Duplicate auth user constraint violation for user {UserId}: {Constraint}",
            userId,
            pgEx.ConstraintName);

        return Errors.General.Duplicate("user");
    }
}
