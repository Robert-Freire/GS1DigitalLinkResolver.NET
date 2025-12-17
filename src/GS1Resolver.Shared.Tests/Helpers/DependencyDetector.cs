using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Net.Http;

namespace GS1Resolver.Shared.Tests.Helpers;

/// <summary>
/// Helper class to detect availability of external dependencies for integration tests.
/// </summary>
public static class DependencyDetector
{
    /// <summary>
    /// Checks if the Cosmos DB emulator is reachable at the configured endpoint.
    /// </summary>
    /// <param name="connectionString">Cosmos DB connection string</param>
    /// <param name="timeoutMs">Timeout in milliseconds (default: 3000)</param>
    /// <returns>True if emulator is reachable, false otherwise</returns>
    public static async Task<bool> IsCosmosEmulatorAvailableAsync(string connectionString, int timeoutMs = 3000)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        try
        {
            using var cts = new CancellationTokenSource(timeoutMs);
            var clientOptions = new CosmosClientOptions
            {
                HttpClientFactory = () =>
                {
                    var httpMessageHandler = new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                    };
                    return new HttpClient(httpMessageHandler);
                },
                ConnectionMode = ConnectionMode.Gateway,
                RequestTimeout = TimeSpan.FromMilliseconds(timeoutMs)
            };

            using var client = new CosmosClient(connectionString, clientOptions);

            // Try to read account properties to verify connectivity
            await client.ReadAccountAsync();
            return true;
        }
        catch (CosmosException)
        {
            // CosmosException indicates the Cosmos service is reachable
            // Database/container might not exist yet, but initializer will create them
            return true;
        }
        catch (HttpRequestException)
        {
            // Network error - emulator not reachable
            return false;
        }
        catch (TaskCanceledException)
        {
            // Timeout - emulator not responding
            return false;
        }
        catch (Exception)
        {
            // Any other error - assume emulator unavailable
            return false;
        }
    }

    /// <summary>
    /// Checks if the GS1 toolkit is available by verifying Node.js and toolkit path.
    /// </summary>
    /// <param name="nodePath">Path to Node.js executable (e.g., "node" or full path)</param>
    /// <param name="toolkitPath">Path to GS1 Digital Link toolkit directory</param>
    /// <param name="timeoutMs">Timeout in milliseconds (default: 5000)</param>
    /// <returns>True if toolkit is available, false otherwise</returns>
    public static async Task<bool> IsGS1ToolkitAvailableAsync(string nodePath, string toolkitPath, int timeoutMs = 5000)
    {
        if (string.IsNullOrWhiteSpace(nodePath) || string.IsNullOrWhiteSpace(toolkitPath))
        {
            return false;
        }

        // Check if Node.js is available
        if (!await IsNodeAvailableAsync(nodePath, timeoutMs))
        {
            return false;
        }

        // Check if toolkit path exists
        try
        {
            var fullToolkitPath = Path.GetFullPath(toolkitPath);
            if (!Directory.Exists(fullToolkitPath))
            {
                return false;
            }

            // Check for key toolkit files (package.json is a good indicator)
            var packageJsonPath = Path.Combine(fullToolkitPath, "package.json");
            if (!File.Exists(packageJsonPath))
            {
                return false;
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if Node.js is available and executable.
    /// </summary>
    /// <param name="nodePath">Path to Node.js executable</param>
    /// <param name="timeoutMs">Timeout in milliseconds</param>
    /// <returns>True if Node.js is available, false otherwise</returns>
    private static async Task<bool> IsNodeAvailableAsync(string nodePath, int timeoutMs)
    {
        try
        {
            using var cts = new CancellationTokenSource(timeoutMs);

            var startInfo = new ProcessStartInfo
            {
                FileName = nodePath,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return false;
            }

            await process.WaitForExitAsync(cts.Token);
            return process.ExitCode == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Gets configuration values with environment variable overrides.
    /// Environment variables take precedence over configuration file values.
    /// </summary>
    /// <param name="configuration">Configuration instance</param>
    /// <param name="key">Configuration key (supports nested keys with ":")</param>
    /// <param name="envVarName">Environment variable name to check</param>
    /// <returns>Configuration value, with env var override if present</returns>
    public static string? GetConfigWithEnvOverride(IConfiguration configuration, string key, string envVarName)
    {
        var envValue = Environment.GetEnvironmentVariable(envVarName);
        if (!string.IsNullOrWhiteSpace(envValue))
        {
            return envValue;
        }

        return configuration[key];
    }
}
