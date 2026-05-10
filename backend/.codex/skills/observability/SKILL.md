---
name: observability
description: Use when adding or changing logging, Loki/Grafana, Prometheus metrics, CorrelationId, or tracing.
---

# Observability Skill

## Logging

Use structured logging:

```csharp
_logger.LogInformation(
    "HLS processing completed. RawVideoId: {RawVideoId}, HlsVideoId: {HlsVideoId}, DepartmentId: {DepartmentId}",
    rawVideoId,
    hlsVideoId,
    departmentId);
```

Do not use string interpolation for structured logs.

## Required properties for workflows

- `CorrelationId`
- business ids
- service name
- operation name
- elapsed time for long operations

## Loki/Grafana

Useful LogQL:

```logql
{service_name="DirectoryService"} | json | level="error"
{service_name="FileService"} | json | level="error"
{service_name=~"FileService|DirectoryService"} | json | CorrelationId="..."
{service_name="DirectoryService"} |= "directory.file.events"
```

## Prometheus

Expose `/metrics` from services.

Useful metrics:

- HTTP request count
- request duration
- active HLS jobs
- HLS processing duration
- failed video processing count
- RabbitMQ queue depth

## Secrets

Never log:

- JWTs
- refresh tokens
- passwords
- S3 access keys
- presigned URLs
