namespace AuthService.Contracts.Requests;

public sealed record GetAuthAuditEventsRequest(
    Guid? CompanyId,
    Guid? UserId,
    string? Action,
    DateTime? CreatedFromUtc,
    DateTime? CreatedToUtc,
    int Page,
    int PageSize);
