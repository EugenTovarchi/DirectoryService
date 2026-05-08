# Backend AI Instructions

Primary entry point for work started from `backend/`.

## Startup

1. Read this file.
2. Read `.codex/rules/*.md`.
   - For Serena MCP usage, see [.codex/rules/serena-mcp.md](.codex/rules/serena-mcp.md).
3. Read [docs/architecture/overview.md](docs/architecture/overview.md).
4. Read only the affected service doc:
   - [docs/services/directory-service.md](docs/services/directory-service.md)
   - [docs/services/file-service.md](docs/services/file-service.md)
   - [docs/architecture/shared-kernel.md](docs/architecture/shared-kernel.md)
5. Read the affected service `AGENTS.md`.
6. Read focused pattern/rule docs as needed:
   - [docs/patterns/configuration.md](docs/patterns/configuration.md)
   - [docs/patterns/docker-config.md](docs/patterns/docker-config.md)
   - [docs/patterns/video-processing.md](docs/patterns/video-processing.md)
   - [docs/rules/coding-style.md](docs/rules/coding-style.md)
   - [docs/rules/domain-rules.md](docs/rules/domain-rules.md)
   - [docs/rules/naming-conventions.md](docs/rules/naming-conventions.md)

## Operating Rules

- Verify from code before editing; do not assume schema, contracts, DTOs, or boundaries.
- Reuse existing FileService/DirectoryService/SharedService patterns before inventing new ones.
- Prefer consistency over new abstractions.
- Keep changes surgical and scoped.
- Keep controllers/endpoints thin and handlers focused.
- Use structured logging and pass `CancellationToken`.
- Preserve public DTO/event compatibility unless explicitly asked for a breaking change.
- Do not put secrets in appsettings, Docker images, logs, or committed config.
- Do not edit generated `bin/` or `obj/` files.

## Workspace Map

- `backend.slnx`: combined Rider solution.
- `DirectoryService/DirectoryService.sln`: DirectoryService solution.
- `FileService/FileService.sln`: FileService solution.
- `SharedService/SharedService.sln`: shared kernel/framework solution.
- `IntegrationEvents/IntegrationEvents.sln`: integration event contracts.
- `../docker-compose-dev.yml`: local Docker compose file.

For adding future services, see [docs/architecture/how-to-add-service.md](docs/architecture/how-to-add-service.md).

## Reporting

For completed work report: plan executed, changed files, verification run, risks/manual checks.
