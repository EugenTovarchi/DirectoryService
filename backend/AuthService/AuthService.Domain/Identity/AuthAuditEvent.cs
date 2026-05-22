using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace AuthService.Domain.Identity;

public sealed class AuthAuditEvent
{
    public const int ACTION_MAX_LENGTH = 120;
    public const int EMAIL_MAX_LENGTH = 320;
    public const int IP_ADDRESS_MAX_LENGTH = 64;
    public const int USER_AGENT_MAX_LENGTH = 512;

    private AuthAuditEvent()
    {
    }

    private AuthAuditEvent(
        Guid? companyId,
        Guid? userId,
        string? email,
        string action,
        Guid? actorUserId,
        string? ipAddress,
        string? userAgent,
        string? metadataJson)
    {
        Id = Guid.NewGuid();
        CompanyId = companyId;
        UserId = userId;
        Email = email;
        Action = action;
        ActorUserId = actorUserId;
        CreatedAt = DateTime.UtcNow;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        MetadataJson = metadataJson;
    }

    public Guid Id { get; private set; }
    public Guid? CompanyId { get; private set; }
    public Guid? UserId { get; private set; }
    public string? Email { get; private set; }
    public string Action { get; private set; } = null!;
    public Guid? ActorUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string? MetadataJson { get; private set; }

    public static Result<AuthAuditEvent, Error> Create(
        Guid? companyId,
        Guid? userId,
        string? email,
        string action,
        Guid? actorUserId = null,
        string? ipAddress = null,
        string? userAgent = null,
        string? metadataJson = null)
    {
        if (string.IsNullOrWhiteSpace(action))
            return Errors.General.ValueIsEmptyOrWhiteSpace("action");

        string normalizedAction = action.Trim();
        if (normalizedAction.Length > ACTION_MAX_LENGTH)
            return Errors.General.ValueIsInvalid("action");

        string? normalizedEmail = Normalize(email, EMAIL_MAX_LENGTH);
        string? normalizedIpAddress = Normalize(ipAddress, IP_ADDRESS_MAX_LENGTH);
        string? normalizedUserAgent = Normalize(userAgent, USER_AGENT_MAX_LENGTH);

        return new AuthAuditEvent(
            companyId,
            userId,
            normalizedEmail,
            normalizedAction,
            actorUserId,
            normalizedIpAddress,
            normalizedUserAgent,
            string.IsNullOrWhiteSpace(metadataJson) ? null : metadataJson.Trim());
    }

    private static string? Normalize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        string normalizedValue = value.Trim();

        return normalizedValue.Length <= maxLength
            ? normalizedValue
            : normalizedValue[..maxLength];
    }
}
