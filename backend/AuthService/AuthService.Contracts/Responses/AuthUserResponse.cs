namespace AuthService.Contracts.Responses;

public record AuthUserResponse(
    Guid Id,
    string Email,
    string Username,
    DateTime CreatedAt,
    DateTime UpdatedAt);
