# Architecture Overview

Start here after `../AGENTS.md`.

This backend workspace is a .NET 9 multi-service system. Current services are DirectoryService and FileService; SharedService provides reusable packages and abstractions. AuthService is planned, not implemented here.

## Workspace Shape

- DirectoryService owns directory data: departments, locations, positions, hierarchy, and assignment of file/video assets to directory entities.
- FileService owns files, media assets, S3/MinIO storage, multipart upload, raw video upload, and HLS/video processing.
- SharedService owns generic, service-neutral kernel/framework pieces used by services.
- Shared messaging contracts must remain compatible across producers and consumers.

See:

- [services.md](services.md)
- [shared-kernel.md](shared-kernel.md)
- [how-to-add-service.md](how-to-add-service.md)
- [../services/directory-service.md](../services/directory-service.md)
- [../services/file-service.md](../services/file-service.md)

## Boundaries

Do not cross service storage boundaries:

- DirectoryService must not read FileService database, S3, or internals.
- FileService must not write DirectoryService database.
- Cross-service communication is by HTTP for direct checks/queries and RabbitMQ/Wolverine for async events.

See [../patterns/configuration.md](../patterns/configuration.md) and [../patterns/docker-config.md](../patterns/docker-config.md) for runtime configuration and Docker rules.

## Implementation Style

Prefer existing patterns over new abstractions:

- DirectoryService uses thin MVC controllers plus commands/queries/handlers.
- FileService uses endpoint classes implementing `IEndpoint` plus workflow handlers.
- Both services use `CSharpFunctionalExtensions` `Result`/`UnitResult` with `SharedService.SharedKernel.Error` and `Failure`.
- Domain models use factories and methods to protect invariants.
- Configuration comes from appsettings, User Secrets in Development, and env files in Docker.

See:

- [../rules/coding-style.md](../rules/coding-style.md)
- [../rules/domain-rules.md](../rules/domain-rules.md)
- [../rules/naming-conventions.md](../rules/naming-conventions.md)
