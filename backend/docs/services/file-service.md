# FileService

See also [../architecture/overview.md](../architecture/overview.md) and [../architecture/services.md](../architecture/services.md).

## Purpose

FileService owns file and media workflows:

- media asset metadata
- multipart upload/download/delete
- S3/MinIO storage abstraction
- raw video handling
- HLS/video processing
- file/media integration events for other services

It must not write DirectoryService database directly.

## Key Aggregates

- `MediaAsset`: base media asset with media data, owner info, storage keys, status, and upload/ready/deleted transitions.
- `VideoAsset`: media asset requiring processing, with raw and HLS storage keys.
- `PhotoAsset`, `PreviewAsset`: direct media variants.
- `VideoProcess`: processing aggregate with ordered `VideoProcessStep` items, status, progress, retry state, metadata, HLS key.

Creation and mutation use factories/methods such as `MediaAsset.CreateForUpload`, `VideoAsset.CreateForUpload`, `VideoProcess.Create`, `StartStep`, `CompleteStep`, `Fail`, and `FinishProcessing`.

## External Dependencies

- PostgreSQL for media assets and video process state.
- S3/MinIO for object storage.
- RabbitMQ/Wolverine for file events.
- Redis for caching where configured.
- Quartz with PostgreSQL persistent store for video processing scheduling and recovery.
- ffmpeg/ffprobe for video metadata/HLS work.
- Seq/Loki/Grafana for logs in local Docker.

## Patterns Used

- `IEndpoint` classes map routes in `FileService.Core.Features`.
- Workflow handlers sit beside endpoint mapping classes.
- Domain factories validate media data and status transitions.
- Repositories and transaction abstractions are in Core/Infrastructure.
- Expected failures use `Error`/`Failure`; endpoint handlers return `EndpointResult` or `UnitResult<Failure>`.
- Video processing uses a pipeline of `IProcessingStepHandler` implementations.

See [../patterns/video-processing.md](../patterns/video-processing.md), [../rules/coding-style.md](../rules/coding-style.md), and [../rules/domain-rules.md](../rules/domain-rules.md).

## Configuration

- `Program.cs` loads environment-specific appsettings and environment variables.
- User Secrets are loaded only in `Development`.
- Docker runtime uses `FileService.Development.env` through compose `env_file`.
- S3 keys, DB passwords, RabbitMQ credentials, and NuGet credentials must stay out of appsettings and images.
- Build-time NuGet credentials are passed as BuildKit secrets only.

JWT validation uses the same `Jwt:Issuer`, `Jwt:Audience`, and signing key as AuthService. The signing key must come from User Secrets, environment variables, or a secret manager; committed `appsettings` keeps it empty. FileService validates access tokens locally and does not call AuthService for every request.

## Authorization

- `POST /files/{mediaAssetId}` requires the `files.read` policy.
- The policy requires a valid AuthService access token containing the `permission=files.read` claim.
- Missing, invalid, or expired tokens return `401 Unauthorized`; an authenticated token without the permission returns `403 Forbidden`.
- Minimal API endpoints declare policies through `.RequireAuthorization(...)`; policy names are kept in `FileAuthorizationPolicies` rather than repeated as string literals.
- Swagger exposes Bearer JWT authorization for local verification.

See [../patterns/configuration.md](../patterns/configuration.md) and [../patterns/docker-config.md](../patterns/docker-config.md).

## Important Domain Flows

- Start multipart upload: validate media data, create `MediaAsset`, request storage upload metadata, persist asset.
- Complete multipart upload: complete storage upload, mark asset uploaded, publish `FileUploaded`, mark direct assets ready, schedule video processing when asset requires processing.
- Video processing: after the upload transaction commits, persistent Quartz schedules work; the pipeline executes ordered step handlers, builds only source-appropriate HLS renditions, supports sources without audio, stores HLS/preview output, marks video ready, and publishes `VideoReady` through the outbox. Periodic reconciliation restores missing triggers, while a Quartz cleanup job removes stale temp directories.
- Delete file: mark asset deleted and publish deletion event where current implementation does so.

Related docs:

- [directory-service.md](directory-service.md)
- [../patterns/video-processing.md](../patterns/video-processing.md)
- [../patterns/docker-config.md](../patterns/docker-config.md)
