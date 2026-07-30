using Microsoft.AspNetCore.Identity;

namespace AuthService.Domain.Identity;

/// <summary>
/// Identity-пользователь AuthService: хранит учетные данные, статус и текущий company context.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    private ApplicationUser()
    {
    }

    public ApplicationUser(
        string email,
        Username username,
        DisplayName? displayName,
        Guid? currentCompanyId)
    {
        Id = Guid.NewGuid();
        Email = email;
        UserName = username.Value;
        DisplayName = displayName;
        CurrentCompanyId = currentCompanyId;
        LockoutEnabled = true;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public DisplayName? DisplayName { get; private set; }
    public bool IsActive { get; private set; } = true;
    public Guid? CurrentCompanyId { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    public void ChangeDisplayName(DisplayName? displayName)
    {
        DisplayName = displayName;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Меняет текущий company context пользователя и обновляет дату изменения.
    /// Проверка права на смену компании остаётся в application layer.
    /// </summary>
    public void ChangeCurrentCompany(Guid? companyId)
    {
        CurrentCompanyId = companyId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }
}
