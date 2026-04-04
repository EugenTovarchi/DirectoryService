using CSharpFunctionalExtensions;
using FileService.Domain;
using FileService.Domain.Assets;
using FileService.Domain.MediaProcessing;
using SharedService.SharedKernel;

namespace FileService.VideoProcessing.Pipeline;

public sealed record ProcessingContext
{
    private const string HLS_SUBDIRECTORY = "hls";

    public required VideoProcess VideoProcess { get; init; }

    public required VideoAsset VideoAsset { get; init; }

    public string? WorkingDirectory { get; private set; }

    public string? HlsOutputDirectory { get; private set; }

    public string? MediaAssetUrl { get; private set; }

    public IReadOnlyList<StorageKey> PreviewKeys => _previewKeys.AsReadOnly();
    public StorageKey? SpritePreviewKey { get; private set; }

    private readonly List<StorageKey> _previewKeys = [];

    public void SetPreviewKeys(List<StorageKey> previewKeys, StorageKey? spriteKey = null)
    {
        _previewKeys.Clear();
        _previewKeys.AddRange(previewKeys);
        SpritePreviewKey = spriteKey;
    }

    public UnitResult<Error> CreateWorkingDirectory()
    {
        try
        {
            WorkingDirectory = Directory.CreateTempSubdirectory("video-processing").FullName;

            HlsOutputDirectory = Path.Combine(WorkingDirectory, HLS_SUBDIRECTORY);
            Directory.CreateDirectory(HlsOutputDirectory);
        }
        catch (Exception ex)
        {
            return Error.Failure("workdir.directory.creation", $"Failed to create workdir directory: {ex.Message}");
        }

        return UnitResult.Success<Error>();
    }

    public void SetMediaAssetUrl(string url)
    {
        MediaAssetUrl = url;
    }

    internal void Cleanup()
    {
        WorkingDirectory = null;
        HlsOutputDirectory = null;
        MediaAssetUrl = null;
    }
}