---
name: full-dev-verification
description: Use after implementing a feature, before PR, after merging branches, or when asked to verify the whole local development system.
---

# Full Dev Verification

## Overview

Sequential verification of the local microservice stack: prerequisites, build, Docker Compose, health checks, logs, and tests.

Stop on failure, diagnose, fix, then re-run the failed step before continuing.

## Steps

### 1. Prerequisites

Check:

```bash
docker info
dotnet --info
```

If `.env` is required:

```bash
test -f .env || echo "MISSING: .env"
```

### 2. Build

```bash
dotnet build DirectoryService/DirectoryService.sln
dotnet build FileService/FileService.sln
dotnet build SharedService/SharedService.sln
dotnet build IntegrationEvents/IntegrationEvents.sln
```

Expected: 0 errors.

Warnings are acceptable unless they indicate broken behavior.

### 3. Docker Compose Up

From `backend/`, the compose file is one level above this folder and its build context is the repository root.

Validate compose syntax first when Docker files changed:

```bash
docker compose -f ../docker-compose-dev.yml config
```

```bash
docker compose -f ../docker-compose-dev.yml up -d --build
```

Then check:

```bash
docker compose -f ../docker-compose-dev.yml ps
```

Expected important services:

- `directory-service` running
- `file-service` running when enabled
- `postgres` healthy/running
- `rabbitmq` healthy/running
- `redis` running
- `minio` running
- `seq` running if configured
- `loki` running
- `grafana` running
- `prometheus` running if configured

### 4. Health checks

Use existing health endpoints if present.

Examples:

```bash
curl -sf http://localhost:9002/health
curl -sf http://localhost:8002/health
curl -sf http://localhost:3100/ready
curl -sf http://localhost:9090/-/ready
```

If no health endpoint exists, use Swagger or a simple API endpoint.

### 5. Logs

Check recent errors:

```bash
docker logs directory-service --tail 100
docker logs file-service --tail 100
docker logs rabbitmq --tail 100
docker logs loki --tail 100
```

Useful Loki queries in Grafana:

```logql
{service_name="DirectoryService"} | json | level="error"
{service_name="FileService"} | json | level="error"
{service_name=~"DirectoryService|FileService"} | json | CorrelationId="..."
```

### 6. Tests

Run relevant tests first:

```bash
dotnet test DirectoryService/DirectoryService.sln
dotnet test FileService/FileService.sln
dotnet test SharedService/SharedService.sln
dotnet test IntegrationEvents/IntegrationEvents.sln
```

If the whole suite is too slow, run affected test projects.

## Fix-and-retry protocol

When any step fails:

1. Read the error carefully.
2. Find the root cause.
3. Fix minimally.
4. Re-run the failed step.
5. Continue only after it passes.

## Reporting

```md
## Verification Results

| Step | Status |
|---|---|
| Prerequisites | ... |
| Build | ... |
| Docker Compose | ... |
| Health Checks | ... |
| Logs | ... |
| Tests | ... |

## Issues Found & Fixed

## Blocked / Manual Actions
```
