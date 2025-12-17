using GS1Resolver.Shared.Configuration;
using GS1Resolver.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace GS1Resolver.Shared.Services;

/// <summary>
/// Service for integrating with GS1 Digital Link toolkit via Node.js subprocess calls.
/// Executes callGS1toolkit.js script to perform validation, compression, and analysis.
/// </summary>
public class GS1ToolkitService : IGS1ToolkitService
{
    private readonly ILogger<GS1ToolkitService> _logger;
    private readonly IProcessExecutor _processExecutor;
    private readonly string _nodePath;
    private readonly string _toolkitPath;
    private readonly string _toolkitScriptPath;
    private const int ProcessTimeoutMilliseconds = 30000; // 30 seconds

    public GS1ToolkitService(
        ILogger<GS1ToolkitService> logger,
        IProcessExecutor processExecutor,
        IOptions<GS1ToolkitSettings> settings)
    {
        _logger = logger;
        _processExecutor = processExecutor;

        var config = settings.Value;
        _nodePath = config.NodePath;
        _toolkitPath = config.ToolkitPath;
        _toolkitScriptPath = config.ToolkitScriptPath;

        // Validate paths on initialization
        if (!File.Exists(_toolkitScriptPath))
        {
            _logger.LogWarning("GS1 toolkit script not found at: {ScriptPath}", _toolkitScriptPath);
        }

        _logger.LogInformation("GS1ToolkitService initialized with NodePath={NodePath}, ToolkitPath={ToolkitPath}",
            _nodePath, _toolkitPath);
    }

    /// <inheritdoc/>
    public async Task<bool> TestDigitalLinkSyntaxAsync(string urlPath)
    {
        if (string.IsNullOrWhiteSpace(urlPath))
        {
            return false;
        }

        try
        {
            _logger.LogDebug("Testing Digital Link syntax: {UrlPath}", urlPath);

            // Call toolkit script without "compress" argument to analyze/validate
            var (exitCode, stdout, stderr) = await ExecuteNodeProcessAsync(
                _toolkitScriptPath,
                new[] { urlPath });

            if (exitCode != 0)
            {
                _logger.LogDebug("Digital Link syntax validation failed for '{UrlPath}'. Error: {Error}",
                    urlPath, stderr);
                return false;
            }

            // Parse JSON response from toolkit
            var result = JsonSerializer.Deserialize<GS1ToolkitResult>(stdout, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            // Valid if we have identifiers (primary keys)
            bool isValid = result?.Identifiers != null && result.Identifiers.Any();

            _logger.LogDebug("Digital Link syntax validation result for '{UrlPath}': {IsValid}",
                urlPath, isValid);

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception testing Digital Link syntax: {UrlPath}", urlPath);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<GS1ToolkitResult> UncompressDigitalLinkAsync(string compressedLink)
    {
        if (string.IsNullOrWhiteSpace(compressedLink))
        {
            return new GS1ToolkitResult
            {
                Success = false,
                Error = "Compressed link cannot be null or empty"
            };
        }

        try
        {
            _logger.LogInformation("Uncompressing Digital Link: {CompressedLink}", compressedLink);

            var startTime = Stopwatch.GetTimestamp();

            var (exitCode, stdout, stderr) = await ExecuteNodeProcessAsync(
                _toolkitScriptPath,
                new[] { compressedLink, "uncompress" });

            var elapsedMs = Stopwatch.GetElapsedTime(startTime).TotalMilliseconds;

            if (exitCode != 0)
            {
                _logger.LogWarning("Digital Link uncompression failed for '{CompressedLink}'. Error: {Error}",
                    compressedLink, stderr);

                return new GS1ToolkitResult
                {
                    Success = false,
                    Error = !string.IsNullOrWhiteSpace(stderr) ? stderr.Trim() : "Uncompression failed"
                };
            }

            // Parse JSON response
            var result = JsonSerializer.Deserialize<GS1ToolkitResult>(stdout, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result == null)
            {
                return new GS1ToolkitResult
                {
                    Success = false,
                    Error = "Failed to parse toolkit response"
                };
            }

            _logger.LogInformation("Successfully uncompressed Digital Link in {ElapsedMs}ms. Input: {Input}, Identifiers: {IdentifierCount}, Qualifiers: {QualifierCount}",
                elapsedMs, compressedLink, result.Identifiers?.Count ?? 0, result.Qualifiers?.Count ?? 0);

            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error for uncompression result: {CompressedLink}", compressedLink);

            return new GS1ToolkitResult
            {
                Success = false,
                Error = $"JSON parsing error: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception uncompressing Digital Link: {CompressedLink}", compressedLink);

            return new GS1ToolkitResult
            {
                Success = false,
                Error = $"Uncompression exception: {ex.Message}"
            };
        }
    }

    /// <inheritdoc/>
    public async Task<GS1ToolkitResult> CompressDigitalLinkAsync(string uncompressedLink)
    {
        if (string.IsNullOrWhiteSpace(uncompressedLink))
        {
            return new GS1ToolkitResult
            {
                Success = false,
                Error = "Uncompressed link cannot be null or empty"
            };
        }

        try
        {
            _logger.LogInformation("Compressing Digital Link: {UncompressedLink}", uncompressedLink);

            var startTime = Stopwatch.GetTimestamp();

            var (exitCode, stdout, stderr) = await ExecuteNodeProcessAsync(
                _toolkitScriptPath,
                new[] { uncompressedLink, "compress" });

            var elapsedMs = Stopwatch.GetElapsedTime(startTime).TotalMilliseconds;

            if (exitCode != 0)
            {
                _logger.LogWarning("Digital Link compression failed for '{UncompressedLink}'. Error: {Error}",
                    uncompressedLink, stderr);

                return new GS1ToolkitResult
                {
                    Success = false,
                    Error = !string.IsNullOrWhiteSpace(stderr) ? stderr.Trim() : "Compression failed"
                };
            }

            // Parse JSON response
            var result = JsonSerializer.Deserialize<GS1ToolkitResult>(stdout, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result == null)
            {
                return new GS1ToolkitResult
                {
                    Success = false,
                    Error = "Failed to parse toolkit response"
                };
            }

            _logger.LogInformation("Successfully compressed Digital Link in {ElapsedMs}ms. Input: {Input}, Output: {Compressed}",
                elapsedMs, uncompressedLink, result.Compressed);

            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error for compression result: {UncompressedLink}", uncompressedLink);

            return new GS1ToolkitResult
            {
                Success = false,
                Error = $"JSON parsing error: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception compressing Digital Link: {UncompressedLink}", uncompressedLink);

            return new GS1ToolkitResult
            {
                Success = false,
                Error = $"Compression exception: {ex.Message}"
            };
        }
    }

    /// <inheritdoc/>
    public async Task<GS1ToolkitResult> AnalyzeDigitalLinkAsync(string digitalLink)
    {
        if (string.IsNullOrWhiteSpace(digitalLink))
        {
            return new GS1ToolkitResult
            {
                Success = false,
                Error = "Digital link cannot be null or empty"
            };
        }

        try
        {
            var startTime = Stopwatch.GetTimestamp();

            // Analyze mode: no third argument (only digitalLink)
            var (exitCode, stdout, stderr) = await ExecuteNodeProcessAsync(
                _toolkitScriptPath,
                new[] { digitalLink });

            var elapsedMs = Stopwatch.GetElapsedTime(startTime).TotalMilliseconds;

            if (exitCode != 0)
            {
                _logger.LogWarning("Digital Link analysis failed for '{DigitalLink}'. Error: {Error}",
                    digitalLink, stderr);

                return new GS1ToolkitResult
                {
                    Success = false,
                    Error = !string.IsNullOrWhiteSpace(stderr) ? stderr.Trim() : "Analysis failed"
                };
            }

            // Parse JSON response
            var result = JsonSerializer.Deserialize<GS1ToolkitResult>(stdout, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result == null)
            {
                return new GS1ToolkitResult
                {
                    Success = false,
                    Error = "Failed to parse toolkit response"
                };
            }

            _logger.LogDebug("Successfully analyzed Digital Link in {ElapsedMs}ms", elapsedMs);

            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error for analysis result: {DigitalLink}", digitalLink);

            return new GS1ToolkitResult
            {
                Success = false,
                Error = $"JSON parsing error: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception analyzing Digital Link: {DigitalLink}", digitalLink);

            return new GS1ToolkitResult
            {
                Success = false,
                Error = $"Analysis exception: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Executes a Node.js process with the specified script and arguments.
    /// </summary>
    /// <param name="scriptPath">Full path to the Node.js script</param>
    /// <param name="arguments">Arguments to pass to the script</param>
    /// <returns>Tuple containing exit code, stdout, and stderr</returns>
    private async Task<(int exitCode, string stdout, string stderr)> ExecuteNodeProcessAsync(
        string scriptPath,
        string[] arguments)
    {
        if (!File.Exists(scriptPath))
        {
            var errorMsg = $"Script not found: {scriptPath}";
            _logger.LogWarning(errorMsg);
            return (-1, string.Empty, errorMsg);
        }

        // Build argument string with proper quoting
        var argList = new List<string> { $"\"{scriptPath}\"" };
        argList.AddRange(arguments.Select(arg => $"\"{arg}\""));
        var argumentString = string.Join(" ", argList);

        _logger.LogDebug("Executing Node.js process: {FileName} {Arguments} (WorkingDir: {WorkingDir})",
            _nodePath, argumentString, _toolkitPath);

        return await _processExecutor.ExecuteAsync(
            _nodePath,
            argumentString,
            _toolkitPath,
            ProcessTimeoutMilliseconds);
    }
}
