using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace GS1Resolver.Shared.Services;

/// <summary>
/// Default implementation of IProcessExecutor that executes real system processes.
/// Handles process lifecycle, output capture, timeouts, and cleanup.
/// </summary>
public class ProcessExecutor : IProcessExecutor
{
    private readonly ILogger<ProcessExecutor> _logger;

    public ProcessExecutor(ILogger<ProcessExecutor> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<(int exitCode, string stdout, string stderr)> ExecuteAsync(
        string fileName,
        string arguments,
        string workingDirectory,
        int timeoutMilliseconds)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory
        };

        _logger.LogDebug("Executing process: {FileName} {Arguments} (WorkingDir: {WorkingDir})",
            fileName, arguments, workingDirectory);

        var output = new StringBuilder();
        var error = new StringBuilder();
        Process? process = null;

        try
        {
            process = new Process { StartInfo = processStartInfo };

            process.OutputDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                    output.AppendLine(args.Data);
            };

            process.ErrorDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                    error.AppendLine(args.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait for process with timeout
            using var cts = new CancellationTokenSource(timeoutMilliseconds);
            await process.WaitForExitAsync(cts.Token);

            var exitCode = process.ExitCode;
            var stdout = output.ToString();
            var stderr = error.ToString();

            _logger.LogDebug("Process completed with exit code {ExitCode}", exitCode);

            return (exitCode, stdout, stderr);
        }
        catch (OperationCanceledException)
        {
            var timeoutError = $"Process timed out after {timeoutMilliseconds}ms";
            _logger.LogError(timeoutError);

            // Explicitly terminate the child process on timeout
            if (process != null && !process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit();
                    _logger.LogDebug("Terminated timed-out process and its child processes");
                }
                catch (Exception killEx)
                {
                    _logger.LogWarning(killEx, "Failed to kill timed-out process");
                }
            }

            // Return partial output captured before timeout instead of empty strings
            var partialStdout = output.ToString();
            var partialStderr = error.ToString();

            return (-1, partialStdout, timeoutError + (partialStderr.Length > 0 ? $"\n{partialStderr}" : string.Empty));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute process: {FileName}", fileName);
            return (-1, string.Empty, $"Process execution failed: {ex.Message}");
        }
        finally
        {
            process?.Dispose();
        }
    }
}
