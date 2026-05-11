---
globs: ["**/Dockerfile*", "docker-compose*.yml", ".gitlab-ci.yml", "nginx*.conf"]
---

# Docker & CI/CD Rules

Detailed context:

- `docs/patterns/docker-config.md`
- `docs/patterns/configuration.md`

## Docker

- Runtime config should come from environment variables or `appsettings.{Environment}.json`.
- Do not bake secrets into Docker images.
- In this repo, run Docker commands from `backend/` with `-f ../docker-compose-dev.yml`.
- `../docker-compose-dev.yml` uses repository root (`..`) as build context.
- Dockerfile `COPY` paths must therefore start from repository root, for example `backend/FileService/...`.
- Inside Docker network, use service names:
  - `postgres:5432`
  - `rabbitmq:5672`
  - `redis:6379`
  - `loki:3100`
  - `prometheus:9090`
  - `file-service:8080`
  - `directory-service:8080`
- From host machine, use mapped ports:
  - `localhost:9002`
  - `localhost:8002`
  - `localhost:3100`
  - `localhost:3000`
- `localhost` inside a container means the same container.
- Do not use `localhost` in `appsettings.Docker.json` for inter-container calls.
- Add `depends_on` for startup order, but remember it does not guarantee app readiness unless healthchecks are used.

## Compose

- Keep local dev compose separate from production compose.
- `docker-compose-dev.yml` may build from local source.
- Production compose should normally use built images, not local `build:` sections.
- Keep ports configurable if they may conflict on developer machines.
- Add short Russian comments to Docker Compose/config files for non-obvious local-dev behavior, storage paths, optional services, and secret usage. Keep comments concise.
- For Dockerfile-only edits, validate syntax first:

```bash
docker compose -f ../docker-compose-dev.yml config
```

- For service image edits, build the touched service:

```bash
docker compose -f ../docker-compose-dev.yml build directory-service
docker compose -f ../docker-compose-dev.yml build file-service
```

## Health

Prefer real app health endpoints when available.

Useful checks:

```bash
curl -sf http://localhost:3100/ready
curl -sf http://localhost:9090/-/ready
```

## CI/CD

- Do not expand secrets into logs.
- Use CI variables/secrets.
- Build, test, package, then deploy.
- Run migrations carefully and with rollback awareness.
