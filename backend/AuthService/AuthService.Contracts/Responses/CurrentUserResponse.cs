namespace AuthService.Contracts.Responses;

public sealed record CurrentUserResponse(
    Guid Id,
    string Email,
    string Username,
    string? DisplayName,
    Guid? CurrentCompanyId,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions);
