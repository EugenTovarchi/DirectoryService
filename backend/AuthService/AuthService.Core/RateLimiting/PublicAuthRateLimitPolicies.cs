namespace AuthService.Core.RateLimiting;

public static class PublicAuthRateLimitPolicies
{
    // Именованные policies держат endpoint mapping читаемым и явно показывают public auth surface под rate limit.
    public const string LOGIN = "public-auth-login";
    public const string TOKEN = "public-auth-token";
    public const string PASSWORD_RESET = "public-auth-password-reset";
    public const string INVITE_RESEND = "public-auth-invite-resend";
}
