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

- Follow the shared error boundary rules in [../.codex/rules/backend-patterns.md](../.codex/rules/backend-patterns.md).
- Keep AuthService-specific auth failure semantics in AuthService-local helpers such as `AuthFailures`.
- If a flow needs a new custom auth error, add it in AuthService first, document the public shape, and keep security-sensitive cases intentionally indistinguishable when needed.

## Pre-Commit Documentation Gate

Follow the shared service pre-commit documentation gate in [../.codex/rules/documentation-maintenance.md](../.codex/rules/documentation-maintenance.md). For AuthService this means:

- Review the final staged/unstaged AuthService diff.
- Update [../docs/services/auth-service.md](../docs/services/auth-service.md) as the main development documentation when behavior, contracts, endpoints, flows, configuration, verification, or backlog changed.
- Update the existing learning notes section in [../docs/services/auth-service.md](../docs/services/auth-service.md) when the change adds learning context. Keep it short: plan, what was done, and how the result affects AuthService.

## Post-Commit Summary

After every AuthService commit, provide the user a short feature-oriented summary for personal notes:

- What AuthService feature/capability changed.
- Why it exists.
- How it affects AuthService or the backend product.

Keep this separate from technical verification output. Do not make errors or implementation minutiae the main focus unless the commit is specifically a bug fix.

## Verification

Start with:

```bash
dotnet build AuthService/AuthService.sln
dotnet test AuthService/AuthService.sln
```
