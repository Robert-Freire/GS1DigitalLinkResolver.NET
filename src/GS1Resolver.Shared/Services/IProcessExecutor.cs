namespace GS1Resolver.Shared.Services;

/// <summary>
/// Interface for executing external processes.
/// Provides abstraction layer for unit testing of process-dependent services.
/// </summary>
public interface IProcessExecutor
{
    /// <summary>
    /// Executes a process asynchronously with the specified file, arguments, and working directory.
    /// </summary>
    /// <param name="fileName">The executable or script to run (e.g., "node", "/usr/bin/node")</param>
    /// <param name="arguments">Command-line arguments to pass to the process</param>
    /// <param name="workingDirectory">Working directory for the process execution</param>
    /// <param name="timeoutMilliseconds">Maximum time to wait for process completion</param>
    /// <returns>Tuple containing exit code, stdout, and stderr</returns>
    Task<(int exitCode, string stdout, string stderr)> ExecuteAsync(
        string fileName,
        string arguments,
        string workingDirectory,
        int timeoutMilliseconds);
}
