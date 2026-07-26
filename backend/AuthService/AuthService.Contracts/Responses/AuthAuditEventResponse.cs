namespace AuthService.Contracts.Responses;

public sealed record AuthAuditEventResponse(
    Guid Id,
    Guid? CompanyId,
    Guid? UserId,
    string? Email,
    string Action,
    Guid? ActorUserId,
    DateTime CreatedAt);
