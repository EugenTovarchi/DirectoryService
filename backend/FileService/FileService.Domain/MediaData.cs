using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace FileService.Domain;

/// <summary>
/// Агрегирует метаданные загружаемого файла.
/// </summary>
public sealed record MediaData
{
    public FileName FileName { get; }
    public ContentType ContentType { get; }
    public long Size { get; }
    public int ExpectedChunkCount { get; }

    private MediaData() { }

    private MediaData(FileName fileName, ContentType contentType, long size, int expectedChunkCount)
    {
        FileName = fileName;
        ContentType = contentType;
        Size = size;
        ExpectedChunkCount = expectedChunkCount;
    }

    public static Result<MediaData, Error> Create(FileName fileName, ContentType contentType, long size, int expectedChunkCount)
    {
        if(size <= 0)
            return Errors.General.ValueIsInvalid("size");

        if(expectedChunkCount <= 0)
            return Errors.General.ValueIsInvalid("expectedChunkCount");

        return new MediaData(fileName, contentType, size, expectedChunkCount);
    }
}