namespace AuthService.Contracts.Responses;

public sealed record UserSummaryResponse(
    Guid UserId,
    string Email,
    string Username,
    string? DisplayName,
    Guid? CompanyId,
    bool IsActive,
    IReadOnlyCollection<string> Roles);
