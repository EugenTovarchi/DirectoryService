namespace AuthService.Domain.Identity;

public static class AuthAuditActions
{
    public const string INVITE_CREATED = "InviteCreated";
    public const string INVITE_RESENT = "InviteResent";
    public const string INVITE_ACCEPTED = "InviteAccepted";
    public const string PASSWORD_RESET_REQUESTED = "PasswordResetRequested";
    public const string PASSWORD_RESET_COMPLETED = "PasswordResetCompleted";
    public const string USER_PROFILE_CHANGED = "UserProfileChanged";
    public const string USER_STATUS_CHANGED = "UserStatusChanged";
    public const string USER_ROLE_CHANGED = "UserRoleChanged";
    public const string SESSION_REVOKED = "SessionRevoked";
    public const string ALL_SESSIONS_REVOKED = "AllSessionsRevoked";
    public const string LOGOUT = "Logout";
}
