namespace AuthService.Contracts.Requests;

public sealed record ClientCredentialsTokenRequest(
    string ClientId,
    string ClientSecret);
