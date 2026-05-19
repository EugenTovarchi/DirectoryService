# AuthService AI Instructions

Use this after root [../AGENTS.md](../AGENTS.md).

## Read First

- [../docs/services/auth-service.md](../docs/services/auth-service.md)
- [../docs/architecture/overview.md](../docs/architecture/overview.md)
- [../docs/architecture/shared-kernel.md](../docs/architecture/shared-kernel.md)
- [../docs/rules/coding-style.md](../docs/rules/coding-style.md)
- [../docs/rules/domain-rules.md](../docs/rules/domain-rules.md)
- [../docs/patterns/configuration.md](../docs/patterns/configuration.md)

## Service Rules

- AuthService owns identity, ASP.NET Core Identity users/roles, access token issuing, refresh token rotation, invites, and auth audit events.
- AuthService does not own company hierarchy, cameras, files, videos, or detailed business access rules.
- Use `CurrentCompanyId` on `ApplicationUser` for MVP; keep multi-company membership for post-MVP.
- Use `AuthService.Contracts` for externally consumed DTOs/contracts.
- Follow the existing `IEndpoint` + handler pattern from FileService.
- Keep `AuthService.Contracts` DTOs stable once consumed externally.
- Do not log passwords, password hashes, JWTs, refresh tokens, or other credentials.
- Do not store real secrets in appsettings or committed config.
- Keep auth documentation updated in [../docs/services/auth-service.md](../docs/services/auth-service.md) when changing AuthService boundaries, token lifecycle, roles, permissions, invites, or sessions.

## Error Rules

- Keep the shared `Error`/`Failure`/`Envelope` response model in SharedService.
- Keep AuthService-specific auth failure semantics in AuthService-local helpers such as `AuthFailures`.
- Handlers may return local `Failure` helpers directly when the method returns `Result<T, Failure>`.
- Do not add AuthService-specific errors to SharedService unless the error is truly service-neutral and useful to other services.
- If a flow needs a new custom auth error, add it in AuthService first, document the public shape, and keep security-sensitive cases intentionally indistinguishable when needed.

## Pre-Commit Documentation Gate

Run this gate only when the final diff is ready and before creating an AuthService commit.

- Review the final staged/unstaged AuthService diff.
- Update [../docs/services/auth-service.md](../docs/services/auth-service.md) as the main development documentation when behavior, contracts, endpoints, flows, configuration, verification, or backlog changed.
- Update the existing learning notes section in [../docs/services/auth-service.md](../docs/services/auth-service.md) when the change adds learning context. Keep it short: plan, what was done, and how the result affects AuthService.
- Do not add commit hashes, commit names, branch names, or long changelog entries to service documentation.
- If no documentation update is needed, state why before committing.
- Do not create the commit until this gate is complete.

## Verification

Start with:

```bash
dotnet build AuthService/AuthService.sln
dotnet test AuthService/AuthService.sln
```
