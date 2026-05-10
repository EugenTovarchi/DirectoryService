using System.Diagnostics;
using System.Text;
using CSharpFunctionalExtensions;
using FileService.Domain;
using Microsoft.Extensions.Logging;
using SharedService.SharedKernel;

namespace FileService.VideoProcessing.ProcessRunner;

// Класс-обёртка над процессом.
public class DataProcessRunner : IDataProcessRunner
{
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
            _logger.LogDebug("STDERR: {Data}", args.Data);
        };

        _logger.LogInformation("Starting process: {FileName} {Arguments}", processCommand.ExecutableFile,
            processCommand.Arguments);

        _logger.LogDebug("Full command: {FileName} {Arguments}",
            processCommand.ExecutableFile, processCommand.Arguments);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Process was canceled: {FileName} {Arguments}", processCommand.ExecutableFile,
                processCommand.Arguments);

            return FileErrors.OperationCancelled();
        }

        var result = new ProcessResult(process.ExitCode, outputBuilder.ToString(), errorBuilder.ToString());

        if (result.ExitCode != 0)
        {
            _logger.LogError("Process failed with exit code {ExitCode}", result.ExitCode);
            _logger.LogError("STDERR: {Error}", result.StandardError);
            _logger.LogError("STDOUT: {Output}", result.StandardOutput);

            return FileErrors.ProcessFailed();
        }

        return result;
    }
}