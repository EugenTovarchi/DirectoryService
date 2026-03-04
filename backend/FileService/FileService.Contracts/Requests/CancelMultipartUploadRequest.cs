namespace FileService.Contracts.Requests;

public record CancelMultipartUploadRequest(Guid MediaAssetId, string UploadId);