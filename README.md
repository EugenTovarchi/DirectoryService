# DirectoryService

DirectoryService is a .NET backend service for managing an organization directory: departments and their hierarchy, locations, positions, and media references assigned to directory entities.

This README focuses on the component in `backend/DirectoryService`. The repository also contains AuthService, FileService, shared infrastructure, and the local Docker environment used by the backend services.

## Responsibilities

DirectoryService supports these use cases:

- create root and child departments;
- move a department and update descendant `ltree` paths;
- read department roots, children, and departments ranked by positions;
- assign locations to a department;
- create and query locations;
- create positions;
- soft-delete departments and clean expired records in a background service;
- assign or clear department media references from FileService events.

DirectoryService owns its PostgreSQL data. It does not access FileService storage, S3, or the FileService database directly. Media details are requested through the FileService contract, while file lifecycle notifications are consumed through RabbitMQ/Wolverine.

## Solution structure

| Project | Responsibility |
|---|---|
| `DirectoryService.Web` | ASP.NET Core entry point, controllers, JWT authorization, Swagger, logging, telemetry, and startup migrations |
| `DirectoryService.Application` | commands, queries, handlers, validation, caching, and integration-event consumers |
| `DirectoryService.Domain` | departments, locations, positions, value objects, and hierarchy invariants |
| `DirectoryService.Contracts` | request, response, and value-object contracts |
| `DirectoryService.Infrastructure.Postgres` | EF Core/Dapper persistence, PostgreSQL repositories, transactions, migrations, and cleanup worker |
| `DirectoryService.IntegrationTests` | API/application-boundary tests with xUnit and Testcontainers |

The solution file is `backend/DirectoryService/DirectoryService.sln`.

## Technology and dependencies

- .NET 9 and ASP.NET Core MVC;
- EF Core with Npgsql and PostgreSQL `ltree` for the department hierarchy;
- Dapper for targeted read and bulk-update queries;
- Redis-backed `HybridCache` for department queries;
- Wolverine with RabbitMQ and PostgreSQL-backed durable messaging;
- JWT bearer authentication with `directory.read` and `directory.manage` permission policies;
- Serilog with console, Seq, and Loki sinks;
- OpenTelemetry metrics/traces with the local collector and Prometheus/Grafana;
- xUnit, `WebApplicationFactory`, Respawn, and Testcontainers for tests.

## Prerequisites

- .NET 9 SDK;
- Docker Desktop for the local stack and integration tests;
- access to the private GitLab NuGet source configured in `nuget.config`;
- local environment files and credentials supplied through the project's established secret-management workflow.

Do not commit NuGet credentials, connection strings, JWT keys, service-client secrets, or tokens. Docker image builds read NuGet credentials through BuildKit secrets backed by the `NUGET_USERNAME` and `NUGET_PASSWORD` environment variables.

## Local start with Docker Compose

From the repository root, validate the resolved Compose model before starting containers:

```powershell
docker compose -f .\docker-compose-dev.yml config --quiet
```

Start the complete local backend stack:

```powershell
docker compose -f .\docker-compose-dev.yml up -d --build
docker compose -f .\docker-compose-dev.yml ps
```

DirectoryService is published at `http://localhost:9002`. Swagger is enabled in the `Docker` environment at `http://localhost:9002/swagger`.

The local stack also exposes PostgreSQL on port `5434`, Redis on `6379`, RabbitMQ management on `15672`, Seq UI on `8081`, Grafana on `3000`, and Prometheus on `9090`. Runtime data is mounted under `D:/docker-data`, outside the repository.

To stop the containers without deleting their persistent data:

```powershell
docker compose -f .\docker-compose-dev.yml stop
```

## Direct development run

The `http` launch profile uses `ASPNETCORE_ENVIRONMENT=Development` and `http://localhost:8001`:

```powershell
dotnet run --project .\backend\DirectoryService\DirectoryService.Web\DirectoryService.Web.csproj --launch-profile http
```

Before this command can start successfully, provide valid `ConnectionStrings:DefaultConnection`, `ConnectionStrings:RabbitMq`, Redis, JWT, and FileService settings through User Secrets or environment variables, and start the required external services. External Wolverine transports are enabled by default.

`backend/DirectoryService/DirectoryService.Web/DirectoryService.Web.http` still contains the template `weatherforecast` request and an obsolete port; it is not a valid smoke test for the current API.

## Configuration

`Program.cs` loads `appsettings.json`, optional `appsettings.{Environment}.json`, Development User Secrets, and environment variables. Environment variables have the highest precedence in that sequence.

Important sections are:

| Section | Purpose |
|---|---|
| `ConnectionStrings:DefaultConnection` | DirectoryService PostgreSQL and Wolverine durable storage |
| `ConnectionStrings:Redis` | distributed cache |
| `ConnectionStrings:RabbitMq` | Wolverine external transport |
| `Jwt` | issuer, audience, metadata endpoint, and HTTPS metadata policy |
| `CacheOptions` | local/distributed cache durations |
| `FileServiceOptions` | FileService endpoints and service-to-service token client |
| `Messaging:UseExternalTransports` | enables RabbitMQ and durable messaging; defaults to `true` |
| `OpenTelemetry` | service resource, metrics, traces, and OTLP exporter |
| `Serilog` and `Logging:Loki` | structured-log levels and sinks |

All current controller actions require an authenticated access token. Read endpoints use the `directory.read` policy; mutation endpoints use `directory.manage`.

## Database migrations

The application calls `Database.MigrateAsync()` during startup in every environment except `Testing`. Existing committed migrations must not be rewritten.

Restore the pinned EF Core tool and create a new migration from the repository root:

```powershell
dotnet tool restore
dotnet ef migrations add <MigrationName> `
  --project .\backend\DirectoryService\DirectoryService.Infrastructure.Postgres\DirectoryService.Infrastructure.Postgres.csproj `
  --startup-project .\backend\DirectoryService\DirectoryService.Web\DirectoryService.Web.csproj `
  --context DirectoryServiceDbContext
```

The startup project still needs valid development configuration when EF Core creates its design-time service provider. Review the generated migration and model snapshot before applying it.

## Build and tests

Build the component solution:

```powershell
dotnet build .\backend\DirectoryService\DirectoryService.sln
```

Run its tests:

```powershell
dotnet test .\backend\DirectoryService\DirectoryService.sln
```

Integration tests start isolated PostgreSQL containers. RabbitMQ topology tests start a pinned RabbitMQ Testcontainer. Docker must be available; tests do not rely on a manually configured developer database.

Run one scenario with a filter when investigating a regression:

```powershell
dotnet test .\backend\DirectoryService\tests\DirectoryService.IntegrationTests\DirectoryService.IntegrationTests.csproj `
  --filter "FullyQualifiedName~MoveDepartmentTests"
```

## Observability and diagnostics

- Serilog emits structured application and request logs.
- Correlation IDs are added to request processing.
- Seq and Loki receive local Docker logs; Grafana is provisioned with repository dashboards.
- OpenTelemetry can export metrics and traces to the local collector.
- Swagger is available only in `Development` and `Docker`.

Useful local diagnostics:

```powershell
docker compose -f .\docker-compose-dev.yml ps
docker logs directory-service --tail 120
docker logs directory-service-postgres --tail 160
```
