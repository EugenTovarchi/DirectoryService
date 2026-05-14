# Docker Configuration Pattern

Use with [configuration.md](configuration.md).

## Build-Time Configuration

Build-time secrets are only for NuGet restore:

- `nuget_config`: BuildKit secret file from root `nuget.config`.
- `nuget_username`: BuildKit secret from environment variable `NUGET_USERNAME`.
- `nuget_password`: BuildKit secret from environment variable `NUGET_PASSWORD`.

Compose reads these build secrets from the host shell environment. Service `env_file`
files are runtime container configuration and do not automatically provide build
secrets.

PowerShell example:

```powershell
$env:NUGET_USERNAME="your-github-username"
$env:NUGET_PASSWORD="your-github-pat"
docker compose -f docker-compose-dev.yml build file-service directory-service auth-service
```

GitHub PAT scopes:

- `read:packages` for Docker builds that restore private packages.
- `repo` when packages or repositories are private.
- `write:packages` only when publishing packages.

Do not commit real PAT values. Prefer ignored local env/script files when needed,
and do not print token values in logs.

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
