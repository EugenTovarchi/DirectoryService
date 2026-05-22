namespace AuthService.Contracts.Responses;

public sealed record CompanyUserResponse(
    Guid UserId,
    string Email,
    string Username,
    string? DisplayName,
    Guid? CompanyId,
    bool IsActive,
    IReadOnlyCollection<string> Roles);
