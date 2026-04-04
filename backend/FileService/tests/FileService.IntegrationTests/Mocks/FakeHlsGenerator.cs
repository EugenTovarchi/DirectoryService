using CSharpFunctionalExtensions;
using FileService.Domain.Assets;
using FileService.Domain.MediaProcessing.VO;
using FileService.VideoProcessing.FfmpegProcess;
using SharedService.SharedKernel;

namespace FileService.IntegrationTests.Mocks;

public class FakeHlsGenerator : IFfmpegProcessRunner
{
    private const int SEGMENTS_PER_QUALITY = 3;
    private static readonly string[] _qualities = ["360p", "720p", "1080p"];

    private readonly VideoMetadata _fakeMetadata = VideoMetadata.Create(
        duration: TimeSpan.FromSeconds(23),
        width: 1080,
        height: 1920).Value;

    public Task<UnitResult<Error>> GenerateHlsAsync(
        string inputFileUrl,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        CreateMasterPlayList(outputDirectory);

        foreach (string quality in _qualities)
        {
            CreateVariantPlaylist(outputDirectory, quality);
            CreateSegments(outputDirectory, quality);
        }

        return Task.FromResult(UnitResult.Success<Error>());
    }

    public Task<Result<VideoMetadata, Error>> ExtractMetadataAsync(
        string inputFileUrl,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Result.Success<VideoMetadata, Error>(_fakeMetadata));

    public Task<UnitResult<Error>> ExtractFrameAsync(
        string inputFileUrl,
        string outputPath,
        TimeSpan timestamp,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<UnitResult<Error>> CreateSpriteSheetAsync(
        List<string> imagePaths,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    private static void CreateMasterPlayList(string outputDirectory)
    {
        string content = """
                         #EXTM3U
                         #EXT-X-VERSION:3

                         #EXT-X-STREAM-INF:BANDWIDTH=2_000_000, RESOLUTION=640x360, NAME="360p"
                         360p_stream.m3u8

                         #EXT-X-STREAM-INF:BANDWIDTH=3_000_000, RESOLUTION=1280x720, NAME="720p"
                         720p_stream.m3u8

                         #EXT-X-STREAM-INF:BANDWIDTH=5_000_000, RESOLUTION=1920x1080, NAME="1080p"
                         1080p_stream.m3u8
                         """;

        string masterPath = Path.Combine(outputDirectory, VideoAsset.MASTER_PLAYLIST_NAME);
        File.WriteAllText(masterPath, content);
    }

    private static void CreateVariantPlaylist(string outputDirectory, string quality)
    {
        string segmentList = string.Join(
            Environment.NewLine,
            Enumerable.Range(1, SEGMENTS_PER_QUALITY)
                .Select(i => $"ETINF:4.000, {Environment.NewLine}{quality}_{i:D6}.ts"));

        string content = $"""
                          #EXTM3U
                          #EXT-X-VERSION:3
                          #EXT-X-TARGETDURATION: 4
                          #EXT-X-MEDIA-SEQUENCE:0
                          #EXT-X-PLAYLIST-TYPE:V0D

                          {segmentList}

                          #EXT-X-ENDLIST
                          """;

        string playlistPath = Path.Combine(outputDirectory, $"{quality}_stream.m3u8");
        File.WriteAllText(playlistPath, content);
    }

    private static void CreateSegments(string outputDirectory, string quality)
    {
        byte[] fakeSegmentData = CreateFakeTsSegment(quality);

        for (int i = 1; i <= SEGMENTS_PER_QUALITY; i++)
        {
            string segmentName = $"{quality}_{i:D6}.ts";
            string segmentPath = Path.Combine(outputDirectory, segmentName);
            File.WriteAllBytes(segmentPath, fakeSegmentData);
        }
    }

    private static byte[] CreateFakeTsSegment(string quality)
    {
        byte[] data = new byte[188 + 18];
        for (int i = 0; i < 10; i++)
        {
            data[i * 188] = 0x47;
        }

        byte[] qualityBytes = System.Text.Encoding.UTF8.GetBytes(quality);
        Array.Copy(qualityBytes, 0, data, 4, Math.Min(qualityBytes.Length, 10));

        return data;
    }
}