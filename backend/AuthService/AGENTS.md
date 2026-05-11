# AuthService AI Instructions

Use this after root [../AGENTS.md](../AGENTS.md).

## Read First

- [../docs/architecture/overview.md](../docs/architecture/overview.md)
- [../docs/architecture/shared-kernel.md](../docs/architecture/shared-kernel.md)
- [../docs/rules/coding-style.md](../docs/rules/coding-style.md)
- [../docs/rules/domain-rules.md](../docs/rules/domain-rules.md)
- [../docs/patterns/configuration.md](../docs/patterns/configuration.md)

## Service Rules

- AuthService owns authentication user data.
- Follow the existing `IEndpoint` + handler pattern from FileService.
- Keep `AuthService.Contracts` DTOs stable once consumed externally.
- Do not log passwords, password hashes, JWTs, refresh tokens, or other credentials.
- Do not store real secrets in appsettings or committed config.

## Verification

Start with:

```bash
dotnet build AuthService/AuthService.sln
dotnet test AuthService/AuthService.sln
```
