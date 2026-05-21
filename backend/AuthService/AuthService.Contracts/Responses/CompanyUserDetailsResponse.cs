namespace AuthService.Contracts.Responses;

public sealed record CompanyUserDetailsResponse(
    Guid UserId,
    string Email,
    string Username,
    string? DisplayName,
    Guid? CompanyId,
    bool IsActive,
    IReadOnlyCollection<string> Roles,
    DateTime CreatedAt,
    DateTime UpdatedAt);
