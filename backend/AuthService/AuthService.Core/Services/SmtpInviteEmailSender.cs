using System.Net;
using System.Net.Mail;
using System.Text;
using AuthService.Core.Abstractions;
using AuthService.Core.Models;
using AuthService.Core.Options;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedService.SharedKernel;

namespace AuthService.Core.Services;

public sealed class SmtpInviteEmailSender : IInviteEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpInviteEmailSender> _logger;

    public SmtpInviteEmailSender(
        IOptions<EmailOptions> options,
        ILogger<SmtpInviteEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<UnitResult<Error>> SendInviteAsync(
        InviteEmailMessage message,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return UnitResult.Success<Error>();

        using SmtpClient client = CreateClient();
        using MailMessage mailMessage = CreateMailMessage(message);

        try
        {
            await client.SendMailAsync(mailMessage, cancellationToken);
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException)
        {
            _logger.LogError(
                ex,
                "Failed to send invite email to user {UserId} at {Email}",
                message.UserId,
                message.Email);

            return Error.Failure("invite.email.delivery.failed", "Invite email delivery failed");
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Invite email sent to user {UserId} at {Email}",
                message.UserId,
                message.Email);
        }

        return UnitResult.Success<Error>();
    }

    private SmtpClient CreateClient()
    {
        SmtpClient client = new(_options.SmtpHost, _options.SmtpPort)
        {
            EnableSsl = _options.EnableSsl
        };

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            client.Credentials = new NetworkCredential(
                _options.Username,
                _options.Password ?? string.Empty);
        }

        return client;
    }

    private MailMessage CreateMailMessage(InviteEmailMessage message)
    {
        MailAddress from = new(_options.FromEmail, _options.FromName, Encoding.UTF8);
        MailAddress to = new(message.Email, message.DisplayName, Encoding.UTF8);

        MailMessage mailMessage = new(from, to)
        {
            Subject = "Your 24Eye invite",
            Body = $"""
                   Hello{FormatDisplayName(message.DisplayName)},

                   Open this link to accept your 24Eye invite and set your password:

                   {message.InviteLink}

                   This link expires at {message.ExpiresAt:O}.
                   """,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8
        };

        return mailMessage;
    }

    private static string FormatDisplayName(string? displayName)
    {
        return string.IsNullOrWhiteSpace(displayName)
            ? string.Empty
            : $" {displayName.Trim()}";
    }
}
