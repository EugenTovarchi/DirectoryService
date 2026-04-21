namespace FileService.Contracts.Requests;

public record StartMultipartUploadRequest(
    string FileName,
    string AssetType,
    string ContentType,
    long Size,
    string OwnerType,
    Guid OwnerId);