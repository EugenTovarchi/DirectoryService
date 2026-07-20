using AuthService.Core.Options;
using Microsoft.Extensions.Options;

namespace AuthService.Infrastructure.Postgres.EmailDelivery;

/// <summary>
/// Проверяет настройки email outbox при старте AuthService, чтобы неверный интервал или retry policy
/// обнаруживались до первого запуска Quartz-задания.
/// </summary>
public sealed class EmailOutboxOptionsValidator : IValidateOptions<EmailOutboxOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailOutboxOptions options)
    {
        List<string> failures = [];

        if (options.PollIntervalSeconds <= 0)
            failures.Add("EmailOutbox:PollIntervalSeconds must be positive");

        if (options.MessagesPerRun <= 0)
            failures.Add("EmailOutbox:MessagesPerRun must be positive");

        if (options.MaxAttempts <= 0)
            failures.Add("EmailOutbox:MaxAttempts must be positive");

        if (options.InitialRetryDelaySeconds <= 0)
            failures.Add("EmailOutbox:InitialRetryDelaySeconds must be positive");

        if (options.MaxRetryDelaySeconds < options.InitialRetryDelaySeconds)
        {
            failures.Add(
                "EmailOutbox:MaxRetryDelaySeconds must be greater than or equal to InitialRetryDelaySeconds");
        }

        if (options.ProcessingLeaseSeconds <= 0)
            failures.Add("EmailOutbox:ProcessingLeaseSeconds must be positive");

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
