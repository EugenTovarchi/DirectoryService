namespace AuthService.Contracts.Requests;

public sealed record InviteUserRequest(
    string Email,
    string Username,
    string? DisplayName,
    Guid CompanyId,
    string Role,
    string InitialPassword);
