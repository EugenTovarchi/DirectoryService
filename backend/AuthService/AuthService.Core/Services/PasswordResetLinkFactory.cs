using AuthService.Core.Options;
using Microsoft.Extensions.Options;

namespace AuthService.Core.Services;

public sealed class PasswordResetLinkFactory
{
    private readonly EmailOptions _options;

    public PasswordResetLinkFactory(IOptions<EmailOptions> options)
    {
        _options = options.Value;
    }

    public Uri Create(string rawPasswordResetToken)
    {
        UriBuilder builder = new(_options.PasswordResetBaseUrl);
        string tokenParameter = $"token={Uri.EscapeDataString(rawPasswordResetToken)}";

        builder.Query = string.IsNullOrWhiteSpace(builder.Query)
            ? tokenParameter
            : $"{builder.Query.TrimStart('?')}&{tokenParameter}";

        return builder.Uri;
    }
}
