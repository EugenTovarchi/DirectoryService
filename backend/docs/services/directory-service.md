# DirectoryService

See also [../architecture/overview.md](../architecture/overview.md) and [../architecture/services.md](../architecture/services.md).

## Purpose

DirectoryService owns directory/domain data:

- departments
- locations
- positions
- department hierarchy
- assignment of media assets to directory entities
- consumption of file-related integration events

It must not access FileService database, S3, or internals directly.

## Key Aggregates

- `Department`: hierarchy node with `Name`, `Identifier`, `Path`, `Depth`, optional parent, video/photo asset ids, locations, and positions.
- `Location`: directory location with department-location links.
- `Position`: directory position with department-position links.
- Join entities: `DepartmentLocation`, `DepartmentPosition`.

Domain creation and mutation use factories/methods such as `Department.CreateRoot`, `Department.CreateChild`, `MoveTo`, `AddLocation`, `UpdateVideoId`, and `Delete`.

## External Dependencies

- PostgreSQL for directory data.
- Redis/HybridCache for cached department reads.
- RabbitMQ/Wolverine for file events.
- FileService HTTP client for media existence/details.
- Seq/Loki/Grafana for logs in local Docker.

## Patterns Used

- MVC controllers stay thin.
- Controllers create command/query records and call handlers.
- Application logic lives under `DirectoryService.Application/Commands`, `Queries`, or `Messaging`.
- Handlers implement SharedService command/query abstractions.
- FluentValidation validates commands.
- Business failures return `Failure`, not exceptions.
- Read-heavy queries use Dapper and `INpgsqlConnectionFactory`.
- EF repositories handle aggregate persistence and locking.

See [../rules/coding-style.md](../rules/coding-style.md) and [../rules/domain-rules.md](../rules/domain-rules.md).

## Configuration

- `Program.cs` loads environment-specific appsettings.
- User Secrets are loaded only in `Development`.
- Environment variables are added after appsettings/User Secrets.
- Docker runtime uses `DirectoryService.Development.env` through compose `env_file`.
- Secrets must stay out of appsettings and image layers.

See [../patterns/configuration.md](../patterns/configuration.md) and [../patterns/docker-config.md](../patterns/docker-config.md).

## Important Domain Flows

- Create department: validate command, create root/child using value objects, verify locations, persist, clear `departments` cache tag.
- Update department locations: transaction, load department, verify distinct location ids, replace department-location rows, save, clear cache.
- Move department: transaction, lock department/parent/descendants, update path/depth/parent, bulk-update descendant ltree paths, clear cache.
- Soft delete: lock hierarchy, mark deleted, update paths, soft-delete uniquely related positions/locations, clear cache.
- Media event handling: `FileUploaded` assigns video/photo asset ids; `FileDeleted` clears matching ids.

Related docs:

- [file-service.md](file-service.md)
- [../patterns/configuration.md](../patterns/configuration.md)
- [../patterns/docker-config.md](../patterns/docker-config.md)
