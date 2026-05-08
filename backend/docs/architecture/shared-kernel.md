# Shared Kernel

SharedService is the service-neutral foundation. It must stay generic.

## What Is Inside

Observed projects:

- `SharedService.Core`: command/query abstractions, pagination, handler helpers, validation helpers.
- `SharedService.Framework`: controller/endpoint helpers and HTTP response envelope behavior.
- `SharedService.SharedKernel`: shared `Error`, `Failure`, `Errors`, `Envelope`, and related result/error types.

Examples used by services:

- `ICommandHandler<TResponse, TCommand>` and `ICommandHandler<TCommand>` return `Result<TResponse, Failure>` or `UnitResult<Failure>`.
- `IQueryHandler<TResponse, TQuery>` returns query response values.
- `ValidationExtensions.ToErrors()` maps FluentValidation failures into `Failure`.
- `ApplicationController.Ok()` wraps responses in `Envelope.Ok(...)`.

## How Services Depend On It

- DirectoryService handlers implement SharedService command/query interfaces and return `Failure`.
- FileService workflow handlers use `Error`, `Failure`, `EndpointResult`, and endpoint helpers.
- Both services use `Errors.General.*`, `Error.Validation(...)`, and `.ToFailure()` rather than throwing for expected business failures.

## Rules For New Services

- Reuse SharedService command/query abstractions before creating service-local variants.
- Reuse `Error`/`Failure` result style for business validation.
- Keep SharedService free of FileService/DirectoryService/AuthService business concepts.
- Prefer additive SharedService changes; public package/API changes require checking all consumers.

Related docs:

- [overview.md](overview.md)
- [services.md](services.md)
- [how-to-add-service.md](how-to-add-service.md)
- [../rules/coding-style.md](../rules/coding-style.md)
- [../rules/domain-rules.md](../rules/domain-rules.md)
