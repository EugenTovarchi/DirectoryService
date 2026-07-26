namespace AuthService.Contracts.Responses;

public sealed record ServiceTokenResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt);
