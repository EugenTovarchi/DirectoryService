namespace AuthService.Core.Models;

public sealed record PasswordResetEmailMessage(
    Guid UserId,
    string Email,
    string? DisplayName,
    Uri ResetLink,
    DateTime ExpiresAt);
