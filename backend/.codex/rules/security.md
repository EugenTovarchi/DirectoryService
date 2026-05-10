---
globs: ["**/appsettings*.json", "**/*.cs", "**/Dockerfile*", "docker-compose*.yml"]
---

# Security Rules

Detailed context:

- `docs/patterns/configuration.md`
- `docs/patterns/docker-config.md`

- Never commit real secrets.
- Do not store S3 access keys, JWT secrets, DB passwords, or API tokens in committed JSON files.
- Use environment variables, `.env`, user secrets, or secret managers.
- Do not log JWTs, refresh tokens, access keys, passwords, or presigned URLs.
- Validate file upload size and content type.
- Do not trust file extensions alone.
- Use parameterized SQL for all raw SQL/Dapper queries.
- Do not leak stack traces to API responses.
- CORS should be explicit.
