using SharedService.SharedKernel.Messaging.Files;
using Wolverine;
using Wolverine.RabbitMQ;

namespace DirectoryService.Application.Messaging;

public static class RabbitMqConfiguration
{
    public static void ConfigureRabbitMq(this WolverineOptions opts, string connectionString)
    {
        opts.UseRabbitMq(new Uri(connectionString)) // Из appsettings.Development.json
            .AutoProvision() // проверяет на старте если очереди\ биндинги и нужно ли их создать.
            .EnableWolverineControlQueues() // лучше включать.
            .UseQuorumQueues() // Все очереди будет quorum

            // Создает новый exchange(имя Exchange в другом сервисе - best practice)
            .DeclareExchange(FileEventsRouting.EXCHANGE, exchange =>
            {
                exchange.ExchangeType = ExchangeType.Topic;
                exchange.IsDurable = true;
            });

        opts.ConfigureFileServiceEventsListener();
    }

    private static void ConfigureFileServiceEventsListener(this WolverineOptions opts)
    {
        opts.ListenToRabbitQueue(MessagingConstants.DIRECTORY_SERVICE_QUEUE_NAME, queue =>
        {
            queue.BindExchange(FileEventsRouting.EXCHANGE, bindingKey: FileEventsRouting.ALL_FILE_UPLOADED);
            queue.BindExchange(FileEventsRouting.EXCHANGE, bindingKey: FileEventsRouting.ALL_FILE_DELETED);

            queue.IsDurable = true;
        });
    }
}