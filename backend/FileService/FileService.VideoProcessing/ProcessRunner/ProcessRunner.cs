using System.Diagnostics;
using System.Text;
using CSharpFunctionalExtensions;
using FileService.Domain;
using Microsoft.Extensions.Logging;
using SharedService.SharedKernel;

namespace FileService.VideoProcessing.ProcessRunner;

// Класс-обёртка над процессом.
public class ProcessRunner : IProcessRunner
{
    private readonly ILogger<ProcessRunner> _logger;

    public ProcessRunner(ILogger<ProcessRunner> logger)
    {
        _logger = logger;
    }

    public async Task<Result<ProcessResult, Error>> RunAsync(
        ProcessCommand command,
        Action<string?> onOutput = null!,
        CancellationToken cancellationToken = default)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = command.ExecutableFile,
            Arguments = command.NormalizedArguments,
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

        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is null)
                return;

            errorBuilder.AppendLine(args.Data);
            onOutput?.Invoke(args.Data);
        };

        _logger.LogInformation("Starting process: {fileName} {arguments}", command.ExecutableFile,
            command.Arguments);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Process was canceled: {fileName} {Arguments}", command.ExecutableFile,
                command.Arguments);
            return FileErrors.OperationCancelled();
        }

        var result = new ProcessResult(process.ExitCode, outputBuilder.ToString(), errorBuilder.ToString());

        if (result.ExitCode != 0)
        {
            _logger.LogError("Process failed: {FileName} {Arguments} ExitCode: {ExitCode} Error: {Error}",
                command.ExecutableFile, command.Arguments, result.ExitCode, result.StandardError);

            return FileErrors.ProcessFailed();
        }

        return result;
    }
}