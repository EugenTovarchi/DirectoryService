using SharedService.SharedKernel;

namespace AuthService.Core.Failures;

public static class AuthFailures
{
    public static Failure InvalidCredentials()
    {
        return Errors.User.InvalidCredentials().ToFailure();
    }

    public static Failure InvalidRefreshToken()
    {
        return InvalidCredentials();
    }

    public static Failure InvalidAuthenticatedUser()
    {
        return InvalidCredentials();
    }
}
