# How To Add A Service

Use this for future services such as AuthService.

## Structure

Follow the current service split unless the new service has a clear reason not to:

- `<Service>.Web`: host, composition, config, endpoint/controller registration.
- `<Service>.Application` or `<Service>.Core`: use cases, commands/queries, handlers, abstractions.
- `<Service>.Domain`: aggregates, value objects, domain rules.
- `<Service>.Contracts`: public request/response DTOs and client contracts.
- `<Service>.Infrastructure.Postgres`: EF Core persistence when PostgreSQL is used.
- Optional infrastructure modules for storage, messaging, external clients, or background processing.
- `tests/<Service>.IntegrationTests` and/or `tests/<Service>.UnitTests`.

## Reuse Existing Patterns

- For controller + CQRS style, mirror [DirectoryService](../services/directory-service.md).
- For endpoint class + handler style, mirror [FileService](../services/file-service.md).
- For command/query/result style, reuse [shared-kernel.md](shared-kernel.md).
- For Docker/config, follow [../patterns/docker-config.md](../patterns/docker-config.md) and [../patterns/configuration.md](../patterns/configuration.md).

## Configuration

- Keep secrets out of appsettings and Docker images.
- Use appsettings for non-secret defaults and shape.
- Use User Secrets only in `Development`.
- Use service-specific `*.Development.env` files through Docker Compose `env_file`.
- Use BuildKit secrets only for build-time NuGet credentials.

## Docker

- Add a service Dockerfile that copies only restore inputs before restore.
- Keep runtime configuration out of `ARG` and image layers.
- Add a compose service with `build.secrets` for NuGet and `env_file` for runtime.
- Do not bind-mount service source over published runtime output unless intentionally running `dotnet watch`.

## Documentation

- Add `docs/services/<new-service>.md`.
- Add service `AGENTS.md` that links to root docs rather than duplicating them.
- Update [services.md](services.md) and [overview.md](overview.md).

Related docs:

- [overview.md](overview.md)
- [services.md](services.md)
- [shared-kernel.md](shared-kernel.md)
- [../rules/naming-conventions.md](../rules/naming-conventions.md)
