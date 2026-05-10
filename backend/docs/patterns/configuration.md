# Configuration Pattern

Use with [docker-config.md](docker-config.md).

## Source Order

Observed service startup:

- appsettings base and environment-specific files define shape and non-secret defaults.
- User Secrets load only in `Development`.
- Environment variables are used for Docker/runtime overrides.

## Key Naming

Use the same logical key across providers:

- User Secrets: `ConnectionStrings:DefaultConnection`
- Docker/env: `ConnectionStrings__DefaultConnection`

Common keys:

- `ConnectionStrings:DefaultConnection`
- `ConnectionStrings:Redis`
- `ConnectionStrings:RabbitMq`
- `S3Options:Endpoint`
- `S3Options:AccessKey`
- `S3Options:SecretKey`
- `S3Options:Region`
- `Seq:ApiKey`
- `Seq:ServerUrl`

## Secrets

Never store these in appsettings or Docker image layers:

- DB passwords
- RabbitMQ credentials
- S3/MinIO access keys
- NuGet PATs
- JWTs/tokens
- presigned URLs

Local IDE runs should use User Secrets. Docker runs should use service-specific `*.Development.env` files.

## New Services

New services should:

- add `UserSecretsId` to the Web project
- call `AddUserSecrets<Program>(optional: true)` only in Development
- keep appsettings secret values empty
- add a service-specific env file for Docker runtime
- use BuildKit secrets only for build-time package restore

Related docs:

- [docker-config.md](docker-config.md)
- [../architecture/how-to-add-service.md](../architecture/how-to-add-service.md)
- [../services/directory-service.md](../services/directory-service.md)
- [../services/file-service.md](../services/file-service.md)
