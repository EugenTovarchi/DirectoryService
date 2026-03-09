using CSharpFunctionalExtensions;
using FileService.Core.FilesStorage;
using Microsoft.Extensions.Options;
using SharedService.SharedKernel;

namespace FileService.Infrastructure.S3;

public class ChunkSizeCalculator : IChunkSizeCalculator
{
    private readonly S3Options _s3Options;

    public ChunkSizeCalculator(IOptions<S3Options> s3Options)
    {
        _s3Options = s3Options.Value;
    }

    public Result<(int ChunkSize, int TotalChunks), Error> CalculateChunkSize(long fileSize)
    {
        if (_s3Options.RecommendedChunkSizeBytes <= 0)
            return Errors.General.ValueMustBePositive("recommendedChunkSizeBytes");

        if (_s3Options.MaxChunks <= 0)
            return Errors.General.ValueMustBePositive("maxChunks");

        // Всегда должно быть первой проверкой.
        if (fileSize <= _s3Options.RecommendedChunkSizeBytes)
            return ((int)fileSize, 1);

        int calculateChunks = (int)Math.Ceiling((double)fileSize / _s3Options.RecommendedChunkSizeBytes);

        int actualChunks = Math.Min(calculateChunks, _s3Options.MaxChunks);

        long chunkSize = (fileSize + actualChunks - 1) / actualChunks;

        return ((int)chunkSize, actualChunks);
    }
}