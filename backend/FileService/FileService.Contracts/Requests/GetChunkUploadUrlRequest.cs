namespace FileService.Contracts.Requests;

public record GetChunkUploadUrlRequest(Guid MediaAssetId, string UploadId, int PartNumber);