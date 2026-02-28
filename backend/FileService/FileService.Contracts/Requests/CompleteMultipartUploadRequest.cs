namespace FileService.Contracts.Requests;

public record CompleteMultipartUploadRequest(Guid MediaAssetId, string UploadId, IReadOnlyList<PartETagDto> PartETags);