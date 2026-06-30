# Video Processing Pattern

Primary service: [../services/file-service.md](../services/file-service.md).

## Flow

1. Complete multipart upload for a video asset.
2. Mark the uploaded `MediaAsset`.
3. Publish `FileUploaded`.
4. Create a pending `VideoProcess` in the upload transaction.
5. Commit the upload and process state.
6. Schedule processing through persistent Quartz after commit.
7. The job starts the first pending step and the pipeline loads `ProcessingContext`.
8. Step handlers run in order:
   - Initialize
   - ExtractMetadata
   - GenerateHls
   - UploadHls
   - GeneratePreview
   - Cleanup
9. Persist progress after steps.
10. Mark video processing complete, set the HLS output key, and persist `VideoReady` in the Wolverine outbox.

## Scheduling And Recovery

- Quartz stores jobs and triggers in PostgreSQL under the `quartz` schema.
- `VideoProcess` remains the source of business status, progress, retry count, metadata, and HLS key.
- Quartz is the source of scheduling state: job identity, triggers, fire time, and cluster recovery.
- Upload transactions never start a job before commit.
- A periodic reconciliation service finds recoverable `PENDING`, `RUNNING`, and retryable `FAILED` processes and restores missing triggers.
- One Quartz worker instance runs at most `VideoProcessingOptions:MaxConcurrentJobs` jobs concurrently.
- `[DisallowConcurrentExecution]` prevents concurrent executions of the same video job key.
- A recurring Quartz cleanup job removes only stale `video-processing*` directories under the OS temp root.

## Domain Objects

- `VideoAsset`: validates upload constraints and controls video processing state.
- `VideoProcess`: owns processing status, step list, progress, retries, metadata, and HLS key.
- `VideoProcessStep`: owns individual step status/progress.
- `ProcessingContext`: carries the asset and process through handlers.

## Handler Pattern

- Step handlers implement `IProcessingStepHandler`.
- `ProcessingPipeline` selects handlers by step name and executes them safely.
- Errors are converted to `Error`, classified as transient or permanent, and persisted on `VideoProcess`.
- Transient failures retry the failed step with capped exponential backoff.
- Permanent failures stop without retry.
- Long operations log `VideoAssetId`, step name/order, progress, and error details.

## Configuration

- ffmpeg/ffprobe paths, retry policy, Quartz persistence, clustering, recovery interval, concurrency, and temp cleanup TTL are configured through `VideoProcessingOptions`.
- Storage uses S3/MinIO configuration from env/User Secrets.
- The FileService runtime image installs ffmpeg/ffprobe and uses executable names instead of host-local paths.

## HLS Renditions

- ffprobe records source dimensions and whether an audio stream exists.
- The HLS ladder includes only `360p`, `720p`, and `1080p` renditions not higher than the source.
- Sources below `360p` keep their original height instead of being upscaled.
- Audio mappings and `var_stream_map` audio entries are omitted when the source has no audio stream.

## Integration Event

- `VideoReady` is owned by `FileService.Contracts` and carries asset/owner ids, HLS key, correlation id, and ready timestamp.
- It is routed through the existing `file.events` exchange as `file.video.ready.{targetEntityType}`.
- The event and final `READY`/`SUCCEEDED` state are persisted by the same EF/Wolverine outbox save.

## Observability

- `CorrelationId` is persisted on `VideoProcess`, copied to Quartz `JobDataMap`, and restored across retries/reconciliation.
- Logs and traces may contain business ids; metrics must use only bounded labels such as step and status.
- The video meter publishes active, completed, failed-attempt, retried, total-duration, and step-duration instruments.
- ffmpeg command arguments and presigned URLs must never be logged.

Related docs:

- [configuration.md](configuration.md)
- [docker-config.md](docker-config.md)
- [../rules/domain-rules.md](../rules/domain-rules.md)
- [../services/file-service.md](../services/file-service.md)
