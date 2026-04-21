using FileService.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using SharedService.SharedKernel.Messaging.Files;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Postgresql;

namespace DirectoryService.Application.Messaging;

public static class WolverineConfiguration
{
    // Метод расширения Host.
    public static void AddWolverine(this WebApplicationBuilder builder)
    {
        string rabbitConnectionString = builder.Configuration.GetConnectionString(ConnectionStringNames.RABBIT_MQ)
                                        ?? throw new InvalidOperationException();
        string postgresConnectionString = builder.Configuration.GetConnectionString(ConnectionStringNames.DATABASE)
                                          ?? throw new InvalidOperationException();

        builder.Host.UseWolverine(opts =>
            {
                opts.ApplicationAssembly = typeof(WolverineConfiguration).Assembly;

                opts.ConfigureDurableMessaging(postgresConnectionString);
                opts.ConfigureRabbitMq(rabbitConnectionString);
                opts.ConfigureStandardErrorPolicies();
            },
            ExtensionDiscovery.ManualOnly); // Чтобы Wolverine не сканировал все сборки. Лучше указывать.
    }

    private static void ConfigureDurableMessaging(this WolverineOptions opts, string postgresConnectionString)
    {
        // Настройка outbox.
        opts.PersistMessagesWithPostgresql(postgresConnectionString, "public");
        opts.UseEntityFrameworkCoreTransactions();

        // Автоматический outbox для всех отправляемых сообщений
        opts.Policies.UseDurableOutboxOnAllSendingEndpoints();

        opts.Policies.UseDurableInboxOnAllListeners();
    }
}