namespace AuthService.Contracts.Responses;

public sealed record ResendInviteResponse(
    Guid UserId,
    string Email,
    string Username,
    string? DisplayName,
    Guid? CompanyId,
    IReadOnlyCollection<string> Roles,
    string InviteToken,
    DateTime InviteTokenExpiresAt);
