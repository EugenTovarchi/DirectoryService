namespace AuthService.Contracts.Requests;

public sealed record RevokeSessionRequest(Guid SessionId);
