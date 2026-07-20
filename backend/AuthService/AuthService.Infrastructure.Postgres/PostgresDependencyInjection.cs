using AuthService.Core.Abstractions;
using AuthService.Core.Database;
using AuthService.Core.Options;
using AuthService.Domain.Identity;
using AuthService.Infrastructure.Postgres.Database;
using AuthService.Infrastructure.Postgres.EmailDelivery;
using AuthService.Infrastructure.Postgres.Queries;
using AuthService.Infrastructure.Postgres.Repositories;
using AuthService.Infrastructure.Postgres.Seeding;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Quartz;
using SharedService.Core.Abstractions;

namespace AuthService.Infrastructure.Postgres;

public static class PostgresDependencyInjection
{
    public static IServiceCollection AddPostgresInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddDatabase(configuration)
            .AddIdentityStores()
            .AddRepositories()
            .AddEmailOutbox(configuration);

        services.Configure<LocalViewerSeedOptions>(
            configuration.GetSection(LocalViewerSeedOptions.SECTION_NAME));

        return services;
    }

    private static IServiceCollection AddDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<NpgsqlDataSource>(sp =>
        {
            string? connectionString = configuration.GetConnectionString(Constants.DEFAULT_CONNECTION);

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("Connection string 'DefaultConnection' is missing or empty");
            }

            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString)
            {
                Name = "auth-service-db",
            };

            dataSourceBuilder.UseLoggerFactory(sp.GetRequiredService<ILoggerFactory>());

            return dataSourceBuilder.Build();
        });

        services.AddDbContext<AuthServiceDbContext>((sp, options) =>
        {
            var hostEnvironment = sp.GetRequiredService<IHostEnvironment>();
            var dataSource = sp.GetRequiredService<NpgsqlDataSource>();

            options.UseNpgsql(dataSource);

            options.LogTo(message =>
            {
                if (message.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("Exception", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"[EF] {message}");
                }
            }, LogLevel.Error);

            if (hostEnvironment.IsDevelopment())
            {
                options.EnableDetailedErrors();
            }
        });

        services.AddScoped<ITransactionManager, TransactionManager>();
        services.AddScoped<INpgsqlConnectionFactory, NpgsqlConnectionFactory>();
        services.AddScoped<AuthIdentitySeeder>();
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

        return services;
    }

    private static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IUserInviteTokenRepository, UserInviteTokenRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<IAuthAuditRepository, AuthAuditRepository>();
        services.AddScoped<IEmailOutboxRepository, EmailOutboxRepository>();
        services.AddScoped<IRolePermissionReader, RolePermissionReader>();

        return services;
    }

    /// <summary>
    /// Регистрирует in-memory Quartz scheduler. Состояние доставки и retry хранится в PostgreSQL outbox,
    /// поэтому Quartz здесь отвечает только за регулярный запуск job.
    /// </summary>
    private static IServiceCollection AddEmailOutbox(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IValidateOptions<EmailOutboxOptions>, EmailOutboxOptionsValidator>();
        services.AddOptions<EmailOutboxOptions>()
            .Bind(configuration.GetSection(EmailOutboxOptions.SECTION_NAME))
            .ValidateOnStart();

        EmailOutboxOptions options = configuration
            .GetSection(EmailOutboxOptions.SECTION_NAME)
            .Get<EmailOutboxOptions>() ?? new EmailOutboxOptions();

        services.AddScoped<EmailOutboxProcessor>();

        if (!options.Enabled)
            return services;

        services.AddQuartz(quartz =>
        {
            quartz.UseInMemoryStore();

            var jobKey = new JobKey(
                EmailOutboxDeliveryJob.JOB_NAME,
                EmailOutboxDeliveryJob.GROUP_NAME);

            quartz.AddJob<EmailOutboxDeliveryJob>(job => job.WithIdentity(jobKey));
            quartz.AddTrigger(trigger => trigger
                .WithIdentity(
                    EmailOutboxDeliveryJob.TRIGGER_NAME,
                    EmailOutboxDeliveryJob.GROUP_NAME)
                .ForJob(jobKey)
                .StartNow()
                .WithSimpleSchedule(schedule => schedule
                    .WithIntervalInSeconds(options.PollIntervalSeconds)
                    .RepeatForever()
                    .WithMisfireHandlingInstructionNextWithExistingCount()));
        });

        services.AddQuartzHostedService(hostedService =>
        {
            hostedService.AwaitApplicationStarted = true;
            hostedService.WaitForJobsToComplete = true;
        });

        return services;
    }

    private static IServiceCollection AddIdentityStores(this IServiceCollection services)
    {
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                // MVP: public registration закрыт, но пароль все равно проверяет Identity.
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;

                // Три неверные попытки пароля включают временную блокировку входа без деактивации аккаунта.
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 3;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<ApplicationRole>()
            .AddSignInManager()
            .AddEntityFrameworkStores<AuthServiceDbContext>()
            .AddDefaultTokenProviders();

        return services;
    }
}
