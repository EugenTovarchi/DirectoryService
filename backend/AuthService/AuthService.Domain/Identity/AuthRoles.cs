namespace AuthService.Domain.Identity;

/// <summary>
/// Стартовый набор ролей для MVP identity model.
/// </summary>
public static class AuthRoles
{
    public const string SYSTEM_ADMIN = "SystemAdmin";
    public const string COMPANY_ADMIN = "CompanyAdmin";
    public const string OPERATOR = "Operator";
    public const string TECHNICIAN = "Technician";
    public const string VIEWER = "Viewer";
}
