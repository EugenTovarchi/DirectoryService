namespace AuthService.Domain.Identity;

/// <summary>
/// Единые ограничения длины для identity-полей, которые используются и в VO, и в EF mapping.
/// </summary>
public static class IdentityFieldLimits
{
    public const int EMAIL_MAX_LENGTH = 320;
    public const int USERNAME_MIN_LENGTH = 3;
    public const int USERNAME_MAX_LENGTH = 50;
    public const int DISPLAY_NAME_MIN_LENGTH = 2;
    public const int DISPLAY_NAME_MAX_LENGTH = 120;
    public const int ROLE_NAME_MAX_LENGTH = 256;
    public const int ROLE_DESCRIPTION_MAX_LENGTH = 500;
    public const int PERMISSION_CODE_MAX_LENGTH = 120;
    public const int PERMISSION_DESCRIPTION_MAX_LENGTH = 500;
    public const int REFRESH_TOKEN_HASH_MAX_LENGTH = 512;
    public const int IP_ADDRESS_MAX_LENGTH = 64;
    public const int USER_AGENT_MAX_LENGTH = 512;
}
