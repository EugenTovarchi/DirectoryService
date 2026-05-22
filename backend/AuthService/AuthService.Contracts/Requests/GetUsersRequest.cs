namespace AuthService.Contracts.Requests;

public sealed record GetUsersRequest(
    int Page,
    int PageSize);
