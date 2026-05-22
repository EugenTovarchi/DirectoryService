namespace AuthService.Contracts.Requests;

public sealed record AcceptInviteRequest(
    string InviteToken,
    string Password);
