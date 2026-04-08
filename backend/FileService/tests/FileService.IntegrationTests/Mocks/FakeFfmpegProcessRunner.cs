using CSharpFunctionalExtensions;
using FileService.Domain.Assets;
using FileService.Domain.MediaProcessing.VO;
using FileService.VideoProcessing.FfmpegProcess;
using SharedService.SharedKernel;

namespace FileService.IntegrationTests.Mocks;

public class FakeFfmpegProcessRunner : IFfmpegProcessRunner
{
    private const int SEGMENTS_PER_QUALITY = 3;
    private static readonly string[] _qualities = ["360p", "720p", "1080p"];

    private readonly VideoMetadata _fakeMetadata = VideoMetadata.Create(
        duration: TimeSpan.FromSeconds(23.914667),
        width: 1080,
        height: 1920).Value;

    public Task<Result<VideoMetadata, Error>> ExtractMetadataAsync(
        string inputFileUrl,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success<VideoMetadata, Error>(_fakeMetadata));
    }

    public Task<UnitResult<Error>> GenerateHlsAsync(
        string inputFileUrl,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        CreateMasterPlaylist(outputDirectory);

        foreach (string quality in _qualities)
        {
            CreateVariantPlaylist(outputDirectory, quality);
            CreateSegments(outputDirectory, quality);
        }

        return Task.FromResult(UnitResult.Success<Error>());
    }

    public Task<UnitResult<Error>> ExtractFrameAsync(
        string inputFileUrl,
        string outputPath,
        TimeSpan timestamp,
        CancellationToken cancellationToken = default)
    {
        string? directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        CreateFakeJpegImage(outputPath);

        return Task.FromResult(UnitResult.Success<Error>());
    }

    public Task<UnitResult<Error>> CreateSpriteSheetAsync(
        List<string> imagePaths,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        string? directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        CreateFakeSpriteSheet(outputPath);

        return Task.FromResult(UnitResult.Success<Error>());
    }

    private static void CreateMasterPlaylist(string outputDirectory)
    {
        string content = """
                         #EXTM3U
                         #EXT-X-VERSION:3

                         #EXT-X-STREAM-INF:BANDWIDTH=2000000, RESOLUTION=640x360, NAME="360p"
                         360p_stream.m3u8

                         #EXT-X-STREAM-INF:BANDWIDTH=3000000, RESOLUTION=1280x720, NAME="720p"
                         720p_stream.m3u8

                         #EXT-X-STREAM-INF:BANDWIDTH=5000000, RESOLUTION=1920x1080, NAME="1080p"
                         1080p_stream.m3u8
                         """;

        string masterPath = Path.Combine(outputDirectory, VideoAsset.MASTER_PLAYLIST_NAME);
        File.WriteAllText(masterPath, content);
    }

    private static void CreateVariantPlaylist(string outputDirectory, string quality)
    {
        var segmentLines = new List<string>();
        for (int i = 1; i <= SEGMENTS_PER_QUALITY; i++)
        {
            segmentLines.Add("#EXTINF:4.000,");
            segmentLines.Add($"{quality}_segment_{i:D6}.ts");
        }

        string content = $"""
                          #EXTM3U
                          #EXT-X-VERSION:3
                          #EXT-X-TARGETDURATION:4
                          #EXT-X-MEDIA-SEQUENCE:0
                          #EXT-X-PLAYLIST-TYPE:VOD

                          {string.Join(Environment.NewLine, segmentLines)}

                          #EXT-X-ENDLIST
                          """;

        string playlistPath = Path.Combine(outputDirectory, $"{quality}_stream.m3u8");
        File.WriteAllText(playlistPath, content);
    }

    private static void CreateSegments(string outputDirectory, string quality)
    {
        for (int i = 1; i <= SEGMENTS_PER_QUALITY; i++)
        {
            string segmentName = $"{quality}_segment_{i:D6}.ts";
            string segmentPath = Path.Combine(outputDirectory, segmentName);
            CreateFakeTsSegment(segmentPath);
        }
    }

    private static void CreateFakeTsSegment(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Create);

        // Минимальный валидный TS пакет
        byte[] tsPacket = new byte[188];
        tsPacket[0] = 0x47;
        tsPacket[1] = 0x40;
        tsPacket[2] = 0x00;
        tsPacket[3] = 0x10;

        for (int i = 0; i < 10; i++)
        {
            fs.Write(tsPacket, 0, tsPacket.Length);
        }
    }

    private static void CreateFakeJpegImage(string outputPath)
    {
        // Минимальный валидный JPEG (1x1 пиксель)
        byte[] jpegData = [0xFF, 0xD8, 0xFF, 0xD9];
        File.WriteAllBytes(outputPath, jpegData);
    }

    private static void CreateFakeSpriteSheet(string outputPath)
    {
        // Минимальный валидный JPEG для спрайт-листа
        byte[] jpegData = [0xFF, 0xD8, 0xFF, 0xD9];
        File.WriteAllBytes(outputPath, jpegData);
    }
}