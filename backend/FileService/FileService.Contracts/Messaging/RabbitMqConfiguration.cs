using SharedService.SharedKernel.Messaging.Files;
using SharedService.SharedKernel.Messaging.Files.Events;
using Wolverine;
using Wolverine.RabbitMQ;

namespace FileService.Contracts.Messaging;

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

        opts.ConfigureFileEventsPublishing();
    }

    private static void ConfigureFileEventsPublishing(this WolverineOptions opts)
    {
        opts.PublishMessagesToRabbitMqExchange<FileUploaded>(
                FileEventsRouting.EXCHANGE,
                message => FileEventsRouting.RoutingKeys.FileUploaded(
                        message.AssetType, message.TargetEntityType))
            .UseDurableOutbox();

        opts.PublishMessagesToRabbitMqExchange<FileDeleted>(
            FileEventsRouting.EXCHANGE,
            message => FileEventsRouting.RoutingKeys.FileDeleted(
                    message.AssetType, message.TargetEntityType))
                .UseDurableOutbox();
    }
}