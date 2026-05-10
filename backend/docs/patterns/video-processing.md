# Video Processing Pattern

Primary service: [../services/file-service.md](../services/file-service.md).

## Flow

1. Complete multipart upload for a video asset.
2. Mark the uploaded `MediaAsset`.
3. Publish `FileUploaded`.
4. Create or load `VideoProcess`.
5. Start the first processing step.
6. Schedule processing through Quartz.
7. Pipeline loads `ProcessingContext`.
8. Step handlers run in order:
   - Initialize
   - ExtractMetadata
   - GenerateHls
   - UploadHls
   - GeneratePreview
   - Cleanup
9. Persist progress after steps.
10. Mark video processing complete and set HLS output key.

## Domain Objects

- `VideoAsset`: validates upload constraints and controls video processing state.
- `VideoProcess`: owns processing status, step list, progress, retries, metadata, and HLS key.
- `VideoProcessStep`: owns individual step status/progress.
- `ProcessingContext`: carries the asset and process through handlers.

## Handler Pattern

- Step handlers implement `IProcessingStepHandler`.
- `ProcessingPipeline` selects handlers by step name and executes them safely.
- Errors are converted to `Error` and persisted on `VideoProcess`.
- Long operations log `VideoAssetId`, step name/order, progress, and error details.

## Configuration

- ffmpeg/ffprobe paths are configured through `VideoProcessingOptions`.
- Storage uses S3/MinIO configuration from env/User Secrets.
- Docker must not rely on host-local file paths.

Related docs:

- [configuration.md](configuration.md)
- [docker-config.md](docker-config.md)
- [../rules/domain-rules.md](../rules/domain-rules.md)
- [../services/file-service.md](../services/file-service.md)
