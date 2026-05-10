# DirectoryService + FileService Integration Rules

## Boundary

DirectoryService and FileService are separate services.

- FileService owns files, media assets, S3/MinIO, uploads, downloads, and video/HLS processing.
- DirectoryService owns departments, locations, positions, hierarchy, and domain assignment of media assets.

Neither service should directly access the other service database or internal storage.

## Communication

- Use synchronous HTTP only for direct request/response checks or commands.
- Use RabbitMQ/Wolverine for async workflows and integration events.
- Keep integration event contracts in shared contract packages/projects.
- Preserve DTO and event payload compatibility unless the user explicitly asks for a breaking change.

## Event rules

- Events should be past tense.
- Include `CorrelationId` where available.
- Include useful business ids when relevant:
  - `FileId`
  - `MediaAssetId`
  - `RawVideoId`
  - `HlsVideoId`
  - `DepartmentId`
  - `MessageId`
- Include timestamps when they help diagnostics, idempotency, or ordering.
- Consumers should be idempotent where practical.

## Change rules

When changing a cross-service contract:

1. Update the event/DTO contract.
2. Update all producers.
3. Update all consumers.
4. Update tests for both sides when behavior changes.
5. Run build/test for both affected solutions.

When changing FileService video/HLS behavior that assigns assets to DirectoryService, use the `video-hls-pipeline` skill.

When changing RabbitMQ/Wolverine contracts or routing, use the `rabbitmq-event` skill.

## Logging

- Use structured logs.
- Log publish and consume operations with business ids.
- Include `CorrelationId` when available.
- Do not log presigned URLs, tokens, passwords, access keys, or connection strings.

## Verification

For DS + FS integration changes:

```bash
dotnet build FileService/FileService.sln
dotnet test FileService/FileService.sln
dotnet build DirectoryService/DirectoryService.sln
dotnet test DirectoryService/DirectoryService.sln
```

If Docker behavior changed:

```bash
docker compose -f ../docker-compose-dev.yml up -d --build
docker compose -f ../docker-compose-dev.yml ps
docker logs directory-service --tail 100
docker logs file-service --tail 100
docker logs rabbitmq --tail 100
```
