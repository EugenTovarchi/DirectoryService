namespace AuthService.Contracts.Responses;

public sealed record InviteUserResponse(
    Guid UserId,
    string Email,
    string Username,
    string? DisplayName,
    Guid CompanyId,
    IReadOnlyCollection<string> Roles,
    DateTime InviteTokenExpiresAt);
