using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using CSharpFunctionalExtensions;
using FileService.Domain;
using Microsoft.Extensions.Logging;
using SharedService.SharedKernel;

namespace FileService.VideoProcessing.ProcessRunner;

// Класс-обёртка над процессом.
public partial class DataProcessRunner : IDataProcessRunner
{
    private const int MAX_LOGGED_STDERR_LENGTH = 4096;

    private readonly ILogger<DataProcessRunner> _logger;

    public DataProcessRunner(ILogger<DataProcessRunner> logger)
    {
        _logger = logger;
    }

    public async Task<Result<ProcessResult, Error>> RunAsync(
        ProcessCommand processCommand,
        Action<string?> onOutput = null!,
        CancellationToken cancellationToken = default)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = processCommand.ExecutableFile,
            Arguments = processCommand.NormalizedArguments,
            RedirectStandardOutput = true, // данные из консоли перенаправляем в наше приложение.
            RedirectStandardError = true, // данные из консоли перенаправляем в наше приложение.
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is null)
                return;

            outputBuilder.AppendLine(args.Data);
            onOutput?.Invoke(args.Data);
        };

        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is null) return;
            errorBuilder.AppendLine(args.Data);
            onOutput?.Invoke(args.Data);
        };

        _logger.LogDebug("Starting external process {FileName}", processCommand.ExecutableFile);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("External process {FileName} was canceled", processCommand.ExecutableFile);

            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(CancellationToken.None);
                }
            }
            catch (Exception killException)
            {
                _logger.LogWarning(killException, "Failed to terminate external process {FileName}",
                    processCommand.ExecutableFile);
            }

            return FileErrors.OperationCancelled();
        }

        var result = new ProcessResult(process.ExitCode, outputBuilder.ToString(), errorBuilder.ToString());

        if (result.ExitCode != 0)
        {
            _logger.LogError(
                "External process {FileName} failed with exit code {ExitCode}. Stderr: {StandardError}",
                processCommand.ExecutableFile,
                result.ExitCode,
                SanitizeProcessOutput(result.StandardError));

            return FileErrors.ProcessFailed();
        }

        return result;
    }

    private static string RedactUrls(string value) => UrlPattern().Replace(value, "[REDACTED_URL]");

    private static string SanitizeProcessOutput(string value)
    {
        string sanitizedValue = RedactUrls(value);
        return sanitizedValue.Length <= MAX_LOGGED_STDERR_LENGTH
            ? sanitizedValue
            : $"{sanitizedValue[..MAX_LOGGED_STDERR_LENGTH]} [TRUNCATED]";
    }

    [GeneratedRegex(@"https?://\S+", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex UrlPattern();
}
