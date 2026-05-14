# Observability Pattern

## SharedService OpenTelemetry

`SharedService.Framework` содержит общий OpenTelemetry setup для backend services.
Сервисы подключают его в composition root:

```csharp
services.AddSharedOpenTelemetry(configuration, fallbackServiceName: "FileService");
```

Это держит instrumentation одинаковым между `FileService`, `DirectoryService` и
`AuthService` и не требует дублировать setup в каждом сервисе. Service-specific
значения приходят из config/env.

OpenTelemetry setup не заменяет Serilog и не отправляет логи в Loki. Логи идут
отдельным потоком:

```text
Docker logs -> Alloy -> Loki -> Grafana
```

Метрики идут через Collector:

```text
backend services -> OTel Collector -> Prometheus -> Grafana
```

Tracing в Docker сейчас намеренно выключен до добавления Tempo.

## Configuration

Базовая форма секции:

```yaml
OpenTelemetry:
  Enabled: true
  ServiceName: "FileService"
  ServiceVersion: "0.0.1"
  Environment: "Docker"
  Otlp:
    Endpoint: "http://otel-collector:4317"
    Protocol: "Grpc"
  Metrics:
    Enabled: true
  Tracing:
    Enabled: false
  HealthChecks:
    ExcludeSuccessful: true
```

`ServiceName` должен быть уникальным и стабильным для сервиса. `Tracing.Enabled`
для Docker остается `false`, пока нет Tempo pipeline.

## Docker Env

Общие переменные:

```text
OpenTelemetry__Enabled=true
OpenTelemetry__Environment=Docker
OpenTelemetry__Otlp__Endpoint=http://otel-collector:4317
OpenTelemetry__Otlp__Protocol=Grpc
OpenTelemetry__Metrics__Enabled=true
OpenTelemetry__Tracing__Enabled=false
OpenTelemetry__HealthChecks__ExcludeSuccessful=true
```

Service-specific значения:

```text
OpenTelemetry__ServiceName=FileService
OpenTelemetry__ServiceVersion=0.0.1

OpenTelemetry__ServiceName=DirectoryService
OpenTelemetry__ServiceVersion=0.0.1

OpenTelemetry__ServiceName=AuthService
OpenTelemetry__ServiceVersion=0.0.1
```

Build-time NuGet secrets описаны отдельно в
[docker-config.md](docker-config.md). Не храните реальные PAT в docs или
tracked config.

## Enabled Metrics

Включены базовые метрики:

- ASP.NET Core HTTP server metrics.
- HttpClient metrics.
- .NET runtime metrics.
- Npgsql metrics через `AddMeter("Npgsql")`.
- EF Core metrics через `AddMeter("Microsoft.EntityFrameworkCore")`.

Prometheus сейчас scrapes только OTel Collector, а не backend services напрямую.
Точные имена метрик зависят от версий пакетов, поэтому для проверки сначала
смотрите доступные names в Prometheus.

Npgsql и EF Core metrics могут появиться только после реальной DB activity.

## NpgsqlDataSource Naming

Для DB metrics используйте стабильные low-cardinality datasource names:

- `FileService`: `file-service-db`
- `DirectoryService`: `directory-service-db`
- `AuthService`: `auth-service-db`

Datasource name нужен, чтобы DB metrics были читаемыми в Prometheus/Grafana.
Не используйте connection string, host, username, password или URL как имя
datasource.

## Healthcheck Filtering

Healthcheck paths подготовлены для trace filtering:

- `/health`
- `/healthz`
- `/ready`
- `/live`
- `/nginx/health`

Сейчас tracing в Docker выключен. Ограничение ASP.NET Core instrumentation:
trace filter вызывается до того, как известен response status, поэтому точное
разделение успешных и failed healthcheck traces может потребовать отдельный
processor или sampler при добавлении Tempo.

## Current Boundaries

Сейчас намеренно не настроено:

- Tempo.
- Tracing в Docker.
- EF Core tracing instrumentation.
- `Npgsql.OpenTelemetry` tracing package.
- Dashboards.
- Custom business metrics.

## SharedService Package Workflow

`SharedService.Framework` потребляется backend services как NuGet package
`istreddev.framework`.

Правила workflow:

- При изменении public/shared behavior bump package version.
- Сначала publish нового package version.
- Потом обновляйте backend `Directory.Packages.props`.
- Не переиспользуйте уже опубликованные package versions.
- Текущий backend использует `istreddev.framework` `0.0.13`.

## Verification

Поднять observability infrastructure:

```powershell
docker compose -f docker-compose-dev.yml up -d otel-collector prometheus grafana
```

Поднять backend services:

```powershell
docker compose -f docker-compose-dev.yml up -d file-service directory-service auth-service
```

Проверить Prometheus:

```text
http://localhost:9090/targets
http://localhost:9090/api/v1/query?query=up
```

Полезные Prometheus queries нужно подбирать по фактическим metric names. Для
сервисного разделения смотрите labels вроде `exported_job`; environment можно
проверить через `target_info`.
