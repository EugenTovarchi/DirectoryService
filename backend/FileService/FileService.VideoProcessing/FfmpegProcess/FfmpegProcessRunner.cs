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
    private readonly VideoProcessingOptions _videoOptions;
    private readonly PreviewOptions _previewOptions;
    private readonly IProcessRunner _processRunner;

    public FfmpegProcessRunner(
        IOptions<VideoProcessingOptions> videoProcessingOptions,
        IProcessRunner processRunner,
        IOptions<PreviewOptions> previewOptions)
    {
        _videoOptions = videoProcessingOptions.Value;
        _previewOptions = previewOptions.Value;
        _processRunner = processRunner;
    }

    public async Task<Result<VideoMetadata, Error>> ExtractMetadataAsync(
        string inputFileUrl,
        CancellationToken cancellationToken = default)
    {
        string arguments = BuildFfprobeArguments(inputFileUrl);
        var command = new ProcessCommand(_videoOptions.FfprobePath, arguments);

        Result<ProcessResult, Error> processResult = await _processRunner.RunAsync(command,
            cancellationToken: cancellationToken);
        if (processResult.IsFailure)
            return processResult.Error;

        return FfprobeOutputParser.Parse(processResult.Value.StandardOutput);
    }

    public async Task<UnitResult<Error>> GenerateHlsAsync(
        string inputFileUrl,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        string arguments = BuildFfmpegHlsArguments(inputFileUrl, outputDirectory);
        var command = new ProcessCommand(_videoOptions.FfmpegPath, arguments);

        Result<ProcessResult, Error> processResult = await _processRunner.RunAsync(command,
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

        Result<ProcessResult, Error> processResult = await _processRunner.RunAsync(
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

        Result<ProcessResult, Error> processResult = await _processRunner.RunAsync(
            command,
            cancellationToken: cancellationToken);

        if (processResult.IsFailure)
            return processResult.Error;

        return UnitResult.Success<Error>();
    }

    private static string BuildFfprobeArguments(string inputFileUrl)
    {
        return $"""
                -v error
                -select_streams v:0
                -show_entries stream=width,height
                -show_entries format=duration
                -of json
                "{inputFileUrl}"
                """;
    }

    private string BuildFfmpegHlsArguments(string inputFileUrl, string outputDirectory)
    {
        string hwaccel = _videoOptions.UseHardwareAcceleration
            ? "-hwaccel cuda -hwaccel_output_format cuda"
            : string.Empty;

        return $"-y -stats -loglevel error {hwaccel} -i \"{inputFileUrl}\" " +
               "-filter_complex \"" +
               "[0:v]split=3[v0][v1][v2]; " +
               "[v0]scale=w=-2:h=360[v0out]; " +
               "[v1]scale=w=-2:h=720[v1out]; " +
               "[v2]scale=w=-2:h=1080[v2out]; " +
               "[0:a]asplit=3[a0][a1][a2]\" " +
               BuildVideoMappings() +
               BuildAudioMappings() +
               "-f hls " +
               "-var_stream_map \"v:0,a:0,name:360p v:1,a:1,name:720p v:2,a:2,name:1080p\" " +
               "-hls_time 4 " + // Длина сегмента видео
               "-hls_list_size 0 " +
               "-hls_segment_type mpegts " +
               "-hls_playlist_type vod " +
               $"=hls_segment_filename {outputDirectory}/{VideoAsset.SEGMENT_FILE_PATTERN} " +
               $"-master_pl_name {VideoAsset.MASTER_PLAYLIST_NAME} " +
               $"{outputDirectory}/{VideoAsset.STREAM_PLAYLIST_PATTERN}";
    }

    private string BuildVideoMappings()
    {
        string encoder = _videoOptions.VideoEncoder;
        string preset = _videoOptions.VideoPreset;

        return $"-map \"[v0out]\" -c:v:0 {encoder} -preset {preset} -b:v:0 2M -maxrate:v:0 2M -bufsize:v:0 2M -g 20" +
               $"-map \"[v1out]\" -c:v:1 {encoder} -preset {preset} -b:v:1 3M -maxrate:v:1 3M -bufsize:v:1 3M -g 20 " +
               $"-map \"[v2out]\" -c:v:2 {encoder} -preset {preset} -b:v:2 5M -maxrate:v:2 5M -bufsize:v:2 5M -g 20 ";
    }

    private string BuildAudioMappings() =>
        "-map \"[a0]\" -c:a:0 aac -b:a:0 96k -ac 2" +
        "-map \"[a1]\" -c:a:1 aac -b:a:1 96k -ac 2" +
        "-map \"[a2]\" -c:a:2 aac -b:a:2 96k -ac 2";

    private static string BuildExtractFrameArguments(
        string inputFileUrl,
        string outputPath,
        TimeSpan timestamp,
        int quality)
    {
        // -frames:v 1 - извлечь только один кадр
        // -q:v - качество JPEG (2 - отличное качество)
        return $"-y -ss {timestamp.TotalSeconds:F3} -i \"{inputFileUrl}\" " +
               $"-frames:v 1 -q:v {quality} \"{outputPath}\"";
    }

    private string BuildSpriteSheetArguments(
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
}