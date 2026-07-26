using Microsoft.Extensions.Logging;
using Quartz;

namespace AuthService.Infrastructure.Postgres.EmailDelivery;

/// <summary>
/// Quartz job, который регулярно запускает один проход по transactional email outbox.
/// Надёжность retry хранится в PostgreSQL, а Quartz отвечает только за расписание запуска.
/// </summary>
[DisallowConcurrentExecution]
public sealed class EmailOutboxDeliveryJob(
    EmailOutboxProcessor processor,
    ILogger<EmailOutboxDeliveryJob> logger)
    : IJob
{
    public const string JOB_NAME = "AuthServiceEmailOutboxDelivery";
    public const string TRIGGER_NAME = "AuthServiceEmailOutboxDeliveryTrigger";
    public const string GROUP_NAME = "AuthServiceEmailDelivery";

    public async Task Execute(IJobExecutionContext context)
    {
        int processedCount = await processor.ProcessBatchAsync(context.CancellationToken);
        logger.LogDebug(
            "Email outbox Quartz job {JobKey} reserved {ProcessedCount} messages for delivery",
            context.JobDetail.Key,
            processedCount);
    }
}
