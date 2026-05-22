using AuthService.Core.Options;
using Microsoft.Extensions.Options;

namespace AuthService.Core.Services;

/// <summary>
/// Фабрика собирает ссылку для приглашения пользователя.
/// </summary>
public sealed class InviteLinkFactory
{
    private readonly EmailOptions _options;

    public InviteLinkFactory(IOptions<EmailOptions> options)
    {
        _options = options.Value;
    }

    public Uri Create(string rawInviteToken)
    {
        UriBuilder builder = new(_options.InviteBaseUrl);
        string tokenParameter = $"token={Uri.EscapeDataString(rawInviteToken)}";

        builder.Query = string.IsNullOrWhiteSpace(builder.Query)
            ? tokenParameter
            : $"{builder.Query.TrimStart('?')}&{tokenParameter}";

        return builder.Uri;
    }
}
