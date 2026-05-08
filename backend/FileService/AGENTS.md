# FileService AI Instructions

Use this after root [../AGENTS.md](../AGENTS.md).

## Read First

- [../docs/services/file-service.md](../docs/services/file-service.md)
- [../docs/architecture/overview.md](../docs/architecture/overview.md)
- [../docs/architecture/shared-kernel.md](../docs/architecture/shared-kernel.md)
- [../docs/rules/coding-style.md](../docs/rules/coding-style.md)
- [../docs/rules/domain-rules.md](../docs/rules/domain-rules.md)

If video/HLS, DirectoryService interaction, Docker, or config is involved, also read:

- [../docs/patterns/video-processing.md](../docs/patterns/video-processing.md)
- [../docs/services/directory-service.md](../docs/services/directory-service.md)
- [../DS-FS.md](../DS-FS.md)
- [../docs/patterns/configuration.md](../docs/patterns/configuration.md)
- [../docs/patterns/docker-config.md](../docs/patterns/docker-config.md)

## Service Rules

- FileService owns file metadata, media assets, multipart upload/download/delete, S3/MinIO, and video/HLS processing.
- Do not write DirectoryService database.
- Follow the existing `IEndpoint` + handler pattern in `FileService.Core.Features`.
- Preserve `FileService.Contracts` DTO compatibility.
- Preserve file/media event compatibility.
- Do not log presigned URLs, tokens, access keys, DB passwords, or connection strings.

## Verification

Start with:

```bash
dotnet build FileService/FileService.sln
dotnet test FileService/FileService.sln
```

Broaden to DirectoryService/SharedService/IntegrationEvents only when touched by the change.
