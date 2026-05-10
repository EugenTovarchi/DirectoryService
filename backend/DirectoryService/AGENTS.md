# DirectoryService AI Instructions

Use this after root [../AGENTS.md](../AGENTS.md).

## Read First

- [../docs/services/directory-service.md](../docs/services/directory-service.md)
- [../docs/architecture/overview.md](../docs/architecture/overview.md)
- [../docs/architecture/shared-kernel.md](../docs/architecture/shared-kernel.md)
- [../docs/rules/coding-style.md](../docs/rules/coding-style.md)
- [../docs/rules/domain-rules.md](../docs/rules/domain-rules.md)

If FileService interaction, Docker, or config is involved, also read:

- [../docs/services/file-service.md](../docs/services/file-service.md)
- [../DS-FS.md](../DS-FS.md)
- [../docs/patterns/configuration.md](../docs/patterns/configuration.md)
- [../docs/patterns/docker-config.md](../docs/patterns/docker-config.md)

## Service Rules

- DirectoryService owns departments, locations, positions, hierarchy, and assignment of media assets to directory entities.
- Do not access FileService database, S3, or internals.
- Keep controllers thin; put behavior in commands, queries, handlers, or messaging handlers.
- Preserve `DirectoryService.Contracts` DTO compatibility.
- Preserve RabbitMQ/Wolverine event compatibility.
- Do not rewrite existing migrations; add corrective migrations only when needed.

## Verification

Start with:

```bash
dotnet build DirectoryService/DirectoryService.sln
dotnet test DirectoryService/DirectoryService.sln
```

Broaden to FileService/SharedService only when touched by the change.
