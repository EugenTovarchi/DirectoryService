using CSharpFunctionalExtensions;
using FileService.Domain.Assets;
using FileService.Domain.MediaProcessing.VO;
using FileService.VideoProcessing.Pipeline.Options;
using FileService.VideoProcessing.ProcessRunner;
using Microsoft.Extensions.Options;
using SharedService.SharedKernel;

namespace FileService.VideoProcessing.FfmpegProcess;

public class FfmpegProcessRunner : IFfmpegProcessRunner
{
    private static readonly Rendition[] _renditions =
    [
        new(360, "2M"),
        new(720, "3M"),
        new(1080, "5M"),
    ];

    private readonly VideoProcessingOptions _videoOptions;
    private readonly PreviewOptions _previewOptions;
    private readonly IDataProcessRunner _dataProcessRunner;

    public FfmpegProcessRunner(
        IOptions<VideoProcessingOptions> videoProcessingOptions,
        IDataProcessRunner dataProcessRunner,
        IOptions<PreviewOptions> previewOptions)
    {
        _videoOptions = videoProcessingOptions.Value;
        _previewOptions = previewOptions.Value;
        _dataProcessRunner = dataProcessRunner;
    }

    public async Task<Result<VideoMetadata, Error>> ExtractMetadataAsync(
        string inputFileUrl,
        CancellationToken cancellationToken = default)
    {
        string arguments = BuildFfprobeArguments(inputFileUrl);
        var command = new ProcessCommand(_videoOptions.FfprobePath, arguments);

        Result<ProcessResult, Error> processResult = await _dataProcessRunner.RunAsync(command,
            cancellationToken: cancellationToken);
        if (processResult.IsFailure)
            return processResult.Error;

        return FfprobeOutputParser.Parse(processResult.Value.StandardOutput);
    }

    public async Task<UnitResult<Error>> GenerateHlsAsync(
        string inputFileUrl,
        string outputDirectory,
        VideoMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        string arguments = BuildFfmpegHlsArguments(inputFileUrl, outputDirectory, metadata);
        var command = new ProcessCommand(_videoOptions.FfmpegPath, arguments);

        Result<ProcessResult, Error> processResult = await _dataProcessRunner.RunAsync(command,
            cancellationToken: cancellationToken);
        if (processResult.IsFailure)
            return processResult.Error;

        return UnitResult.Success<Error>();
    }

    public async Task<UnitResult<Error>> ExtractFrameAsync(
        string inputFileUrl,
        string outputPath,
        TimeSpan timestamp,
        CancellationToken cancellationToken = default)
    {
        string? directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        string arguments = BuildExtractFrameArguments(
            inputFileUrl,
            outputPath,
            timestamp,
            _previewOptions.Quality);

        var command = new ProcessCommand(_videoOptions.FfmpegPath, arguments);

        Result<ProcessResult, Error> processResult = await _dataProcessRunner.RunAsync(
            command,
            cancellationToken: cancellationToken);

        if (processResult.IsFailure)
            return processResult.Error;

        return UnitResult.Success<Error>();
    }

    public async Task<UnitResult<Error>> CreateSpriteSheetAsync(
        List<string> imagePaths,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        if (imagePaths == null || imagePaths.Count == 0)
            return Error.Validation("sprite.no.images", "No images provided for sprite sheet");

        // Если только одно изображение, просто копируем его
        if (imagePaths.Count == 1)
        {
            string? folder = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            File.Copy(imagePaths[0], outputPath, overwrite: true);
            return UnitResult.Success<Error>();
        }

        string? directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        string arguments = BuildSpriteSheetArguments(
            imagePaths,
            outputPath,
            _previewOptions.FrameWidth,
            _previewOptions.FrameHeight,
            _previewOptions.Quality);

        var command = new ProcessCommand(_videoOptions.FfmpegPath, arguments);

        Result<ProcessResult, Error> processResult = await _dataProcessRunner.RunAsync(
            command,
            cancellationToken: cancellationToken);

        if (processResult.IsFailure)
            return processResult.Error;

        return UnitResult.Success<Error>();
    }

    private static string BuildFfprobeArguments(string inputFileUrl)
    {
        return
            $"-v error -show_entries stream=codec_type,width,height -show_entries format=duration -of json \"{inputFileUrl}\"";
    }

    private string BuildFfmpegHlsArguments(
        string inputFileUrl,
        string outputDirectory,
        VideoMetadata metadata)
    {
        string hwaccel = _videoOptions.UseHardwareAcceleration
            ? "-hwaccel cuda -hwaccel_output_format cuda"
            : string.Empty;

        string normalizedInputUrl = NormalizePath(inputFileUrl);
        string normalizedOutputDir = NormalizePath(outputDirectory);

        string segmentPattern = CombineAndNormalize(normalizedOutputDir, VideoAsset.SEGMENT_FILE_PATTERN);
        string streamPlaylistPattern = CombineAndNormalize(normalizedOutputDir, VideoAsset.STREAM_PLAYLIST_PATTERN);

        Rendition[] renditions = GetRenditions(metadata.Height);
        string splitOutputs = string.Concat(Enumerable.Range(0, renditions.Length).Select(index => $"[v{index}]"));
        string scaleFilters = string.Join("; ", renditions.Select((rendition, index) =>
            $"[v{index}]scale=w=-2:h={rendition.Height}[v{index}out]"));
        string filterComplex = $"[0:v:0]split={renditions.Length}{splitOutputs}; {scaleFilters}";
        string streamMap = string.Join(" ", renditions.Select((rendition, index) => metadata.HasAudio
            ? $"v:{index},a:{index},name:{rendition.Height}p"
            : $"v:{index},name:{rendition.Height}p"));

        return $"-y -stats -loglevel error {hwaccel} -i \"{normalizedInputUrl}\" " +
               $"-filter_complex \"{filterComplex}\" " +
               BuildVideoMappings(renditions) +
               BuildAudioMappings(renditions.Length, metadata.HasAudio) +
               "-f hls " +
               $"-var_stream_map \"{streamMap}\" " +
               "-hls_time 4 " +
               "-hls_list_size 0 " +
               "-hls_segment_type mpegts " +
               "-hls_playlist_type vod " +
               $"-hls_segment_filename \"{segmentPattern}\" " +
               $"-master_pl_name \"{VideoAsset.MASTER_PLAYLIST_NAME}\" " +
               $"\"{streamPlaylistPattern}\"";
    }

    private string BuildVideoMappings(IReadOnlyList<Rendition> renditions)
    {
        string encoder = _videoOptions.VideoEncoder;
        string preset = _videoOptions.VideoPreset;

        return string.Concat(renditions.Select((rendition, index) =>
            $"-map \"[v{index}out]\" -c:v:{index} {encoder} -preset {preset} " +
            $"-b:v:{index} {rendition.Bitrate} -maxrate:v:{index} {rendition.Bitrate} " +
            $"-bufsize:v:{index} {rendition.Bitrate} -g 20 "));
    }

    private static string BuildAudioMappings(int renditionCount, bool hasAudio)
    {
        return hasAudio
            ? string.Concat(Enumerable.Range(0, renditionCount).Select(index =>
                $"-map 0:a:0 -c:a:{index} aac -b:a:{index} 96k -ac 2 "))
            : string.Empty;
    }

    private static Rendition[] GetRenditions(int sourceHeight)
    {
        Rendition[] renditions = _renditions.Where(rendition => rendition.Height <= sourceHeight).ToArray();
        return renditions.Length > 0
            ? renditions
            : [new Rendition(sourceHeight, "1M")];
    }

    private static string BuildExtractFrameArguments(
        string inputFileUrl,
        string outputPath,
        TimeSpan timestamp,
        int quality)
    {
        string seconds = timestamp.TotalSeconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);

        // -frames:v 1 - извлечь только один кадр
        // -q:v - качество JPEG (2 - отличное качество)
        return $"-y -ss {seconds} -i \"{inputFileUrl}\" " +
               $"-frames:v 1 -q:v {quality} \"{outputPath}\"";
    }

    private static string BuildSpriteSheetArguments(
        List<string> imagePaths,
        string outputPath,
        int frameWidth,
        int frameHeight,
        int quality)
    {
        int totalFrames = imagePaths.Count;

        // Оптимальное расположение кадров в сетке (квадратная или прямоугольная)
        int cols = (int)Math.Ceiling(Math.Sqrt(totalFrames));
        int rows = (int)Math.Ceiling((double)totalFrames / cols);

        // Строим входные файлы
        string inputs = string.Join(" ", imagePaths.Select(p => $"-i \"{p}\""));

        // Строим filter_complex для создания сетки
        var filterParts = new List<string>();

        // Масштабируем все кадры до одинакового размера
        for (int i = 0; i < totalFrames; i++)
        {
            filterParts.Add($"[{i}:v]scale={frameWidth}:{frameHeight}[v{i}]");
        }

        // Собираем кадры в сетку с помощью xstack
        string gridInputs = string.Join(string.Empty, Enumerable.Range(0, totalFrames).Select(i => $"[v{i}]"));

        // Строим layout для xstack (позиционирование кадров)
        var layouts = new List<string>();
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                int index = (row * cols) + col;
                if (index >= totalFrames) break;

                int x = col * frameWidth;
                int y = row * frameHeight;
                layouts.Add($"{x}_{y}");
            }
        }

        string layout = string.Join("|", layouts);
        filterParts.Add($"{gridInputs}xstack=inputs={totalFrames}:layout={layout}[grid]");

        // Применяем filter_complex
        string filterComplex = string.Join(";", filterParts);

        // Собираем полную команду
        return $"{inputs} -filter_complex \"{filterComplex}\" -map \"[grid]\" " +
               $"-frames:v 1 -q:v {quality} \"{outputPath}\"";
    }

    /// <summary>
    /// Нормализует путь для Windows (заменяет \ на /)
    /// </summary>
    private static string NormalizePath(string path)
    {
        return path?.Replace("\\", "/") ?? string.Empty;
    }

    /// <summary>
    /// Объединяет пути и нормализует результат
    /// </summary>
    private static string CombineAndNormalize(params string[] parts)
    {
        string combined = Path.Combine(parts);
        return NormalizePath(combined);
    }

    private sealed record Rendition(int Height, string Bitrate);
}
