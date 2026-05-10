using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using SharedService.SharedKernel.Messaging.Files;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Postgresql;

namespace FileService.Contracts.Messaging;

public static class WolverineConfiguration
{
    // Метод расширения Host.
    public static void AddWolverine(this WebApplicationBuilder builder)
    {
        bool useExternalTransports = builder.Configuration.GetValue("Messaging:UseExternalTransports", true);

        builder.Host.UseWolverine(opts =>
            {
                opts.ApplicationAssembly = typeof(WolverineConfiguration).Assembly;

                if (useExternalTransports)
                {
                    string postgresConnectionString = builder.Configuration.GetConnectionString(ConnectionStringNames.DATABASE)
                                                      ?? throw new InvalidOperationException(
                                                          "Connection string 'DefaultConnection' is required for Wolverine durable messaging.");
                    string rabbitConnectionString = builder.Configuration.GetConnectionString(ConnectionStringNames.RABBIT_MQ)
                                                    ?? throw new InvalidOperationException(
                                                        "Connection string 'RabbitMq' is required when Messaging:UseExternalTransports is enabled.");

                    opts.ConfigureDurableMessaging(postgresConnectionString);
                    opts.ConfigureRabbitMq(rabbitConnectionString);
                }

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
