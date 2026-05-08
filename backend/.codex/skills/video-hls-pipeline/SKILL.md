---
name: video-hls-pipeline
description: Use when changing video upload, CompleteUpload, ffmpeg/HLS processing, HLS asset creation, and DirectoryService assignment.
---

# Video/HLS Pipeline

## Expected flow

```text
CompleteUpload request
↓
FileService validates uploaded raw asset
↓
HLS processing starts
↓
HLS asset is created
↓
FileService publishes event with HlsVideoId
↓
DirectoryService consumes event
↓
DirectoryService assigns HLS video id to department/entity
```

## Logging requirements

Log key stages with structured properties:

- `CorrelationId`
- `RawVideoId`
- `HlsVideoId`
- `DepartmentId`
- `FileName`
- `DurationMs`
- `ProcessingStatus`

## Rabbit event requirements

Event must include:

- `CorrelationId`
- `HlsVideoId`
- target entity id, e.g. `DepartmentId`
- event timestamp

## Common bugs

- raw asset id used instead of HLS asset id
- message published before HLS processing completes
- missing correlation id in RabbitMQ event
- ffmpeg path invalid in Docker
- local file path used inside container
- MinIO endpoint uses localhost inside Docker

## Verification

Use Grafana:

```logql
{service_name=~"FileService|DirectoryService"} | json | CorrelationId="..."
```

Check that logs show:

- HLS processing started
- HLS processing completed
- event published
- event consumed
- department updated
