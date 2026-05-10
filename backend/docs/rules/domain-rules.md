# Domain Rules

Use this with [coding-style.md](coding-style.md).

## Aggregates

- Keep invariants inside aggregate factories and methods.
- Use private/protected constructors for EF and controlled creation.
- Mutate state through methods that validate transitions.
- Update timestamps inside domain methods when state changes.
- Keep collection backing fields private and expose read-only copies.

## DirectoryService Domain

- `Department` controls hierarchy fields (`Path`, `Depth`, `ParentId`) through `CreateRoot`, `CreateChild`, and `MoveTo`.
- Department location/position links are created through domain methods or join factories.
- Soft deletion updates entity state and hierarchy paths through application/repository flows.

See [../services/directory-service.md](../services/directory-service.md).

## FileService Domain

- `MediaAsset` controls upload/ready/deleted state transitions.
- `VideoAsset` validates video size, extension, content type, owner id/type, and processing state.
- `VideoProcess` controls step ordering, progress, retry, failure, and completion.

See [../services/file-service.md](../services/file-service.md) and [../patterns/video-processing.md](../patterns/video-processing.md).

## Shared Errors

- Domain methods return `Error`/`UnitResult<Error>` rather than throwing for expected invalid input.
- Application handlers convert domain errors to `Failure`.

Related docs:

- [coding-style.md](coding-style.md)
- [naming-conventions.md](naming-conventions.md)
- [../architecture/shared-kernel.md](../architecture/shared-kernel.md)
