# Services

This file is the service index. For implementation detail, read only the affected service doc.

## Current Services

- [DirectoryService](../services/directory-service.md): directory hierarchy, departments, locations, positions, asset assignment.
- [FileService](../services/file-service.md): file metadata, media assets, multipart upload/download/delete, S3/MinIO, video/HLS processing.
- [SharedService](shared-kernel.md): shared abstractions, result/error model, endpoint/controller helpers, validation helpers.

## Planned Services

- AuthService: not implemented yet. New service work should follow [how-to-add-service.md](how-to-add-service.md) and reuse patterns documented in [../rules/coding-style.md](../rules/coding-style.md).

## Cross-Service Rules

- Keep ownership clear; do not share databases.
- Preserve DTO/event compatibility unless a breaking change is explicitly requested.
- Use shared contracts/events for RabbitMQ/Wolverine workflows.
- Keep service-specific business logic out of SharedService.

Related docs:

- [overview.md](overview.md)
- [shared-kernel.md](shared-kernel.md)
- [../patterns/configuration.md](../patterns/configuration.md)
- [../patterns/docker-config.md](../patterns/docker-config.md)
