namespace AuthService.Contracts.Requests;

public sealed record ResetPasswordRequest(
    string Token,
    string Password);
