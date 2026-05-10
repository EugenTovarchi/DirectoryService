# Codex Workspace Notes

This folder is configured for Codex work on the backend monorepo.

## Entry Points

- Start every task from `AGENTS.md`.
- Read `.codex/rules/*.md` after `AGENTS.md`.
- Read `docs/architecture/overview.md` for system context.
- Then read only the service-level file for the affected project:
  - `DirectoryService/AGENTS.md`
  - `FileService/AGENTS.md`
  - `SharedService/AGENTS.md`
- Read `DS-FS.md` when work crosses DirectoryService and FileService.

Detailed context lives under `docs/`:

- `docs/architecture/`
- `docs/services/`
- `docs/rules/`
- `docs/patterns/`

## Local Solution

`backend.slnx` is the combined Rider solution for day-to-day work with DirectoryService and FileService.
The individual service solutions still exist and should be used for targeted build/test commands.

## Docker

Local compose is one level above this directory:

```bash
docker compose -f ../docker-compose-dev.yml ps
```

The compose build context is the repository root (`..`), so Dockerfile copy paths start with `backend/...`.
See `docs/patterns/docker-config.md` and `docs/patterns/configuration.md`.

## Useful Local Skills

- `full-dev-verification`
- `add-api-endpoint`
- `rabbitmq-event`
- `observability`
- `video-hls-pipeline`
- `update-shared-nuget`

## Useful Role Docs

- `.codex/agents/architect.md`
- `.codex/agents/debugger.md`
- `.codex/agents/microservice-reviewer.md`
- `.codex/agents/security-reviewer.md`
- `.codex/agents/test-runner.md`

These are reference prompts/docs. The root and service `AGENTS.md` files remain the source of truth.
