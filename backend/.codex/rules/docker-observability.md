---
globs: ["docker-compose*.yml", "backend/infrastructure/observabillity/**", "infrastructure/observabillity/**"]
---

# Docker Observability Rules

- Observability configs live under `backend/infrastructure/observabillity`.
- Keep Alloy, Loki, and Grafana configs service-neutral for backend services and future frontend services.
- In infrastructure config files, add short Russian comments only for important blocks or non-obvious settings.
- Do not use `latest` or broad floating tags in Docker Compose files.
- Pin explicit stable image versions.
- Prefer explicit stable `alpine` tags only when the official image provides them.
- If an official alpine tag is unavailable or uncertain, use an explicit stable non-latest tag and report why.
- Store all Docker bind-mounted persistent data only under `D:/docker-data`.
- Repository config bind mounts are allowed for local configuration files, but runtime data and logs must not persist in the repo.
- Mount Docker socket only for local-dev log collection, and document the security risk in comments or docs.
