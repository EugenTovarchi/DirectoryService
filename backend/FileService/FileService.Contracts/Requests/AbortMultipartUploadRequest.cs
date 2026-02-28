namespace FileService.Contracts.Requests;

public record AbortMultipartUploadRequest(Guid MediaAssetId, string UploadId);