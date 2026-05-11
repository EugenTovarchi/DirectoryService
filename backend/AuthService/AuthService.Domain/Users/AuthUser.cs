using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace AuthService.Domain.Users;

public sealed class AuthUser
{
    private AuthUser()
    {
    }

    private AuthUser(
        Guid id,
        Email email,
        Username username,
        PasswordHash passwordHash)
    {
        Id = id;
        Email = email;
        Username = username;
        PasswordHash = passwordHash;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; private set; }
    public Email Email { get; private set; } = null!;
    public Username Username { get; private set; } = null!;
    public PasswordHash PasswordHash { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public static Result<AuthUser, Error> Create(
        Email email,
        Username username,
        PasswordHash passwordHash)
    {
        if (email is null)
            return Errors.General.ValueIsInvalid("email");

        if (username is null)
            return Errors.General.ValueIsInvalid("username");

        if (passwordHash is null)
            return Errors.General.ValueIsInvalid("passwordHash");

        return new AuthUser(Guid.NewGuid(), email, username, passwordHash);
    }
}
