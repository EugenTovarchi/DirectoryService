using FileService.Domain;

namespace FileService.Core.Models;

public record MediaUrl(StorageKey StorageKey, string PresignedUrl);