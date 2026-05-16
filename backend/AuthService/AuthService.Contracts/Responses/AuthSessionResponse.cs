namespace AuthService.Contracts.Responses;

public sealed record AuthSessionResponse(
    Guid Id,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    DateTime? LastUsedAt,
    string? CreatedByIp,
    string? UserAgent);
