using System.Text.RegularExpressions;

namespace FileService.VideoProcessing.ProcessRunner;

public partial record ProcessCommand(string ExecutableFile, string Arguments)
{
    public string NormalizedArguments => NormalizeWhiteSpace(Arguments);

    private static string NormalizeWhiteSpace(string input) => WhitespaceRegex().Replace(input.Trim(), " ");

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();
}

public record ProcessResult(int ExitCode, string StandardOutput, string StandardError);