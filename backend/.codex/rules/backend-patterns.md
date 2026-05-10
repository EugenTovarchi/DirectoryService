---
globs: ["backend/**/*.cs"]
---

# Backend Patterns

Detailed context:

- `docs/rules/coding-style.md`
- `docs/rules/domain-rules.md`
- `docs/rules/naming-conventions.md`
- `docs/architecture/shared-kernel.md`

## General

- Keep controllers thin.
- Put business/application logic in handlers.
- Use existing Result/Error style for business failures.
- Pass `CancellationToken`.
- Use structured logging.
- Do not use string interpolation in logs.

## Naming

- Async methods end with `Async`.
- Constants use `UPPER_SNAKE_CASE` only if existing code does.
- Private fields follow existing project style.

## Domain

- Value objects validate through `Create`.
- Entities should protect invariants.
- Prefer methods on domain entities over external mutation.

## EF Core

- Use `AsNoTracking` for read-only queries when appropriate.
- Avoid N+1.
- Use indexes for frequent filters.
- Raw SQL must be parameterized.

## Messaging

- Integration events are past tense.
- Include `CorrelationId`.
- Log publish and consume with business ids.
