using CSharpFunctionalExtensions;
using FileService.Domain.MediaProcessing.VO;
using FileService.VideoProcessing.FfmpegProcess;
using FileService.VideoProcessing.Pipeline.Options;
using FileService.VideoProcessing.ProcessRunner;
using FluentAssertions;
using Microsoft.Extensions.Options;
using SharedService.SharedKernel;

namespace FileService.UnitTests;

public class FfmpegProcessRunnerTests
{
    [Fact]
    public async Task GenerateHls_For720pVideoWithoutAudio_ShouldNotUpscaleOrMapAudio()
    {
        var processRunner = new CapturingProcessRunner();
        var runner = CreateRunner(processRunner);
        VideoMetadata metadata = VideoMetadata.Create(TimeSpan.FromMinutes(1), 1280, 720, hasAudio: false).Value;
        string outputDirectory = CreateTempDirectory();

        try
        {
            (await runner.GenerateHlsAsync("input.mp4", outputDirectory, metadata)).IsSuccess.Should().BeTrue();

            string arguments = processRunner.Command!.NormalizedArguments;
            arguments.Should().Contain("h=360").And.Contain("h=720");
            arguments.Should().NotContain("h=1080");
            arguments.Should().NotContain("-map 0:a:0");
            arguments.Should().NotContain(",a:0,");
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task GenerateHls_ForVideoWithAudio_ShouldMapAudioForEachRendition()
    {
        var processRunner = new CapturingProcessRunner();
        var runner = CreateRunner(processRunner);
        VideoMetadata metadata = VideoMetadata.Create(TimeSpan.FromMinutes(1), 1920, 1080, hasAudio: true).Value;
        string outputDirectory = CreateTempDirectory();

        try
        {
            await runner.GenerateHlsAsync("input.mp4", outputDirectory, metadata);

            string arguments = processRunner.Command!.NormalizedArguments;
            arguments.Should().Contain("-map 0:a:0");
            arguments.Should().Contain("v:2,a:2,name:1080p");
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void ParseFfprobeOutput_ShouldDetectMissingAudioStream()
    {
        const string ffprobeOutput =
            """
            {"streams":[{"codec_type":"video","width":640,"height":360}],"format":{"duration":"12.5"}}
            """;

        Result<VideoMetadata, Error> result = FfprobeOutputParser.Parse(ffprobeOutput);

        result.IsSuccess.Should().BeTrue();
        result.Value.HasAudio.Should().BeFalse();
    }

    private static FfmpegProcessRunner CreateRunner(IDataProcessRunner processRunner)
    {
        return new FfmpegProcessRunner(
            Options.Create(new VideoProcessingOptions()),
            processRunner,
            Options.Create(new PreviewOptions()));
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ffmpeg-runner-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class CapturingProcessRunner : IDataProcessRunner
    {
        public ProcessCommand? Command { get; private set; }

        public Task<Result<ProcessResult, Error>> RunAsync(
            ProcessCommand processCommand,
            Action<string?> onOutput = null!,
            CancellationToken cancellationToken = default)
        {
            Command = processCommand;
            return Task.FromResult(Result.Success<ProcessResult, Error>(new ProcessResult(0, string.Empty, string.Empty)));
        }
    }
}
