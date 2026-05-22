namespace AuthService.Core.Models;

public sealed record InviteEmailMessage(
    Guid UserId,
    string Email,
    string? DisplayName,
    Uri InviteLink,
    DateTime ExpiresAt);
