namespace FileService.Contracts.Responses;

public record StartMultipartUploadResponse(
    Guid MediaAssetId,
    string UploadId,
    IReadOnlyList<ChunkUploadUrl>ChunkUploadUrls,
    int ChunkSize);