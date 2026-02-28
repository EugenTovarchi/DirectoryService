namespace FileService.Contracts.Responses;

public record GetChunkUploadUrlResponse(string UploadUrl, int PartNumber);