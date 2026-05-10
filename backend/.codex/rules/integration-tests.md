---
globs: ["**/IntegrationTests/**", "**/tests/**"]
---

# Integration Test Rules

## Infrastructure

- Use `WebApplicationFactory<Program>` where applicable.
- Use Testcontainers for PostgreSQL/Redis/MinIO/RabbitMQ when integration behavior requires real infrastructure.
- Use Respawn or equivalent DB cleanup between tests if available.
- Do not depend on developer machine services for integration tests.

## Patterns

- Test through public API or application boundary where possible.
- Avoid testing EF Core internals.
- Seed only required data.
- Keep tests isolated.
- Use deterministic IDs only when needed.

## RabbitMQ/Wolverine

- Disable external transports in tests unless the test specifically verifies messaging.
- If testing messaging, use real RabbitMQ container.
- Verify publish and consume behavior with timeouts, not infinite waits.

## Common pitfalls

- Docker Desktop not running.
- Connection string points to `localhost` from inside container.
- Tests leak DB state.
- SharedService package version mismatch.
- RabbitMQ URI empty or invalid during registration.
