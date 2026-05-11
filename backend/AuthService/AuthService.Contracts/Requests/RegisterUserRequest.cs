namespace AuthService.Contracts.Requests;

public record RegisterUserRequest(
    string Email,
    string Username,
    string PasswordHash);
