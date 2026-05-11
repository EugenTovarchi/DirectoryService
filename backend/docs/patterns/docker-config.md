# Docker Configuration Pattern

Use with [configuration.md](configuration.md).

## Build-Time Configuration

Build-time secrets are only for NuGet restore:

- `nuget_config`: BuildKit secret file from root `nuget.config`.
- `nuget_username`: BuildKit secret from environment variable `NUGET_USERNAME`.
- `nuget_password`: BuildKit secret from environment variable `NUGET_PASSWORD`.

Dockerfiles must not:

- `COPY nuget.config`
- use `ARG NUGET_USERNAME` or `ARG NUGET_PASSWORD`
- parse runtime env files during build
- bake runtime secrets into image layers

## Runtime Configuration

Runtime configuration comes from compose `env_file`:

- `DirectoryService.Development.env`
- `FileService.Development.env`

Inside Docker, services use Docker DNS names:

- `postgres`
- `rabbitmq`
- `redis`
- `minio`
- `loki`
- `directory-service`
- `file-service`

Do not use `localhost` inside containers for another service.

## Compose Rules

- Compose file is `../docker-compose-dev.yml` when working from `backend/`.
- Compose build context is repository root (`..`).
- Dockerfile `COPY` paths start with `backend/...`.
- Do not mount service source over the runtime app directory unless intentionally using a dev/watch container.
- Add short Russian comments to Docker Compose/config files when they clarify non-obvious local-dev behavior, storage paths, optional services, or secret usage. Keep comments brief and avoid restating obvious YAML keys.

Related docs:

- [configuration.md](configuration.md)
- [../architecture/overview.md](../architecture/overview.md)
- [../services/directory-service.md](../services/directory-service.md)
- [../services/file-service.md](../services/file-service.md)
