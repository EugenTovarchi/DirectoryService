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

## Readability

- Tests are maintained as project documentation: use explicit scenario names and keep Arrange, Act, and Assert easy to identify.
- Prefer separate `[Fact]` tests for distinct business and security scenarios such as missing, invalid, expired, forbidden, and successful authorization cases.
- Use `[Theory]` only when parameterization makes the intent clearer and the cases differ only by simple input data. Do not combine semantically different scenarios merely to remove duplicated setup.
- Prefer explicit test helpers such as `CreateExpiredToken()` over boolean switches such as `CreateToken(expired: true)` when the named helper makes the scenario easier to read.

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
