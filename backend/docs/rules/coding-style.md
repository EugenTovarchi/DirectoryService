# Coding Style

Use this with [domain-rules.md](domain-rules.md) and [naming-conventions.md](naming-conventions.md).

## General

- Prefer small, surgical changes.
- Follow KISS: prefer the simplest implementation that clearly expresses the intent.
- Write code for a reader seeing it for the first time. Prefer explicit control flow and named intermediate variables over compact or clever expressions.
- Do not use bitwise operations as arithmetic shortcuts. Use a readable `switch`, `if`, or named calculation unless bit manipulation is part of the actual domain problem.
- Avoid nested ternary expressions. A single ternary is appropriate only when both outcomes are short and immediately understandable.
- Name all private instance and static fields in `_camelCase`, including `readonly` fields. Keep public properties and other public members in `PascalCase`.
- Inspect existing service patterns before adding new code.
- Keep controllers/endpoints thin.
- Put business workflow logic in handlers/services.
- Pass `CancellationToken` through async calls.
- Use structured logging placeholders, not string interpolation.
- Do not throw for expected business validation failures.
- Prefer explicit type names for simple and readable types such as `string`, `int`, `Guid`, `bool`, `DateTime`, arrays/collections like `string[]` and `IReadOnlyCollection<string>`, short class names, and short interface names. Use `var` when the explicit type is long/noisy, obvious from object creation, an anonymous type, or framework-heavy results such as FluentValidation validation results.
- Name endpoint, handler, command, and validator classes after the concrete operation they perform, not only after the resource. Prefer names like `RotateRefreshTokenHandler`, `RevokeRefreshTokenHandler`, or `CreateSessionHandler` over generic names like `RefreshTokenHandler` when several operations can exist for the same resource.

## Result/Error Style

Observed style:

- Domain factories return `Result<T, Error>` or `UnitResult<Error>`.
- Application handlers return `Result<T, Failure>` or `UnitResult<Failure>`.
- Convert validation errors with `validationResult.ToErrors()`.
- Convert domain/application errors with `.ToFailure()` where needed.
- Use `Errors.General.*` and `Error.Validation(...)` for known failures.

## Constructor and DI Style

- Most handlers use constructor injection with private readonly fields.
- Some infrastructure services use primary constructors where already present.
- Follow the nearby class style rather than standardizing globally.

## Validation

- Use FluentValidation for request/command validation where the service already does.
- Use domain value object factories such as `Name.Create`, `Identifier.Create`, `StorageKey.Create`, and `MediaData` factories for invariant checks.

## Data Access

- Use EF Core repositories for aggregate persistence and mutations.
- Use Dapper/query connections for read models where current code already does.
- Use transactions through service-local transaction abstractions.
- Use `AsNoTracking` for read-only EF queries when appropriate.
- In EF Core entity configurations, do not hard-code `HasMaxLength(...)` values. Reference named constants from the owning domain/value object when available; if no suitable constant exists, add one near the owning type or a focused service-local configuration constants class before using it.

Related docs:

- [domain-rules.md](domain-rules.md)
- [naming-conventions.md](naming-conventions.md)
- [../architecture/shared-kernel.md](../architecture/shared-kernel.md)
- [../services/directory-service.md](../services/directory-service.md)
- [../services/file-service.md](../services/file-service.md)
