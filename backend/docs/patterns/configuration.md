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

## Options Validation

- Options-класс хранит configuration values, безопасные defaults и короткие комментарии о назначении настроек. Не помещайте в него DI-регистрацию или infrastructure behavior.
- Если options имеют несколько связанных проверок или проверка содержит business/infrastructure смысл, создавайте отдельный `IValidateOptions<TOptions>` вместо длинной inline-цепочки `.Validate(...)`.
- Размещайте validator рядом с owning feature/options: например, email outbox validator рядом с email delivery infrastructure, а rate-limit validator рядом с rate limiting.
- DI extension должен оставаться composition root: зарегистрировать `IValidateOptions<TOptions>`, выполнить `Bind(...)` и `ValidateOnStart()`.
- Один короткий и очевидный guard допустимо оставить inline, но при росте набора правил переносите весь набор в один именованный validator.
- Сообщения validation failure должны содержать полный configuration key, чтобы startup error сразу показывал, какую настройку исправить.

Related docs:

- [docker-config.md](docker-config.md)
- [../architecture/how-to-add-service.md](../architecture/how-to-add-service.md)
- [../services/directory-service.md](../services/directory-service.md)
- [../services/file-service.md](../services/file-service.md)
