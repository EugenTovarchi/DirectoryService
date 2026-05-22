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

## Error Boundaries

- Keep shared response primitives such as `Error`, `Failure`, `Envelope`, and result-to-HTTP mapping in SharedService.
- Keep service-specific error semantics in the service that owns the behavior.
- Add local service helpers for repeated service-specific failures instead of duplicating ad hoc private methods in each handler.
- Handlers may return local `Failure` helpers directly when the method returns `Result<T, Failure>`.
- Do not add service-specific errors to SharedService unless the error is truly service-neutral and useful to multiple services.
- If a service-specific flow needs a new public error shape, document it in the affected service docs and keep security-sensitive cases intentionally indistinguishable when required.

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
