using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace FileService.Core.FilesStorage;

public interface IChunkSizeCalculator
{
    Result<(int ChunkSize, int TotalChunks), Error> CalculateChunkSize(long fileSize);
}