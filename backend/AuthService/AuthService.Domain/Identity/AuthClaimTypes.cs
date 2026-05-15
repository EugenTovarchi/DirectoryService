namespace AuthService.Domain.Identity;

/// <summary>
/// Claim names, которые AuthService кладет в JWT и downstream-сервисы используют в authorization policies.
/// </summary>
public static class AuthClaimTypes
{
    public const string COMPANY_ID = "company_id";
    public const string ROLE = "role";
    public const string PERMISSION = "permission";
}
