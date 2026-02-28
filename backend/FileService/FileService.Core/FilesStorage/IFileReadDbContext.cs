using FileService.Domain.Assets;

namespace FileService.Core.FilesStorage;

public interface IFileReadDbContext
{
    IQueryable<MediaAsset> ReadMediaAssets { get; }
}