---
globs: ["**/Migrations/**"]
---

# Migration Rules

- Do not delete, rename, or modify existing committed migration files unless explicitly instructed.
- To fix a migration issue, create a new corrective migration.
- Use EF Core migration transactions where possible.
- Use `migrationBuilder.Sql()` for indexes or SQL features not expressible via Fluent API.
- Be careful with data-destructive changes.
- Cross-schema references must be guarded if schema may not exist in test environments.

## Generate migrations

Use the actual service paths.

Example:

```bash
dotnet ef migrations add <Name> \
  --project backend/DirectoryService/DirectoryService.Infrastructure.Postgres \
  --startup-project backend/DirectoryService/DirectoryService.Web
```
