using GS1Resolver.Shared.Configuration;
using GS1Resolver.Shared.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace GS1Resolver.Shared.Tests.Services;

/// <summary>
/// Integration tests for GS1ToolkitService that run against actual Node.js and toolkit scripts.
/// These tests require Node.js to be installed and GS1 toolkit scripts to be available.
/// Use [Trait("Category", "Integration")] to allow selective execution in CI/CD.
/// </summary>
[Trait("Category", "Integration")]
public class GS1ToolkitServiceIntegrationTests : IDisposable
{
    private readonly Mock<ILogger<GS1ToolkitService>> _serviceLoggerMock;
    private readonly Mock<ILogger<ProcessExecutor>> _executorLoggerMock;
    private readonly string _testToolkitPath;
    private readonly string _encoderScriptPath;
    private readonly string _toolkitScriptPath;
    private readonly bool _skipTests;

    public GS1ToolkitServiceIntegrationTests()
    {
        _serviceLoggerMock = new Mock<ILogger<GS1ToolkitService>>();
        _executorLoggerMock = new Mock<ILogger<ProcessExecutor>>();

        // Determine toolkit path based on environment
        // In Docker: /app/gs1-digitallink-toolkit
        // In development: relative to repository root
        var possiblePaths = new[]
        {
            "/app/gs1-digitallink-toolkit",
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "data_entry_server", "src", "gs1-digitallink-toolkit"),
            Path.Combine(Directory.GetCurrentDirectory(), "gs1-digitallink-toolkit")
        };

        _testToolkitPath = possiblePaths.FirstOrDefault(Directory.Exists) ?? string.Empty;
        _encoderScriptPath = Path.Combine(_testToolkitPath, "callGS1encoder.js");
        _toolkitScriptPath = Path.Combine(_testToolkitPath, "callGS1toolkit.js");

        // Skip tests if Node.js or toolkit scripts are not available
        _skipTests = !IsNodeAvailable() || !File.Exists(_encoderScriptPath) || !File.Exists(_toolkitScriptPath);
    }

    private bool IsNodeAvailable()
    {
        try
        {
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "node",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private GS1ToolkitService CreateService()
    {
        var settings = new GS1ToolkitSettings
        {
            NodePath = "node",
            ToolkitPath = _testToolkitPath,
            ToolkitScriptName = "callGS1toolkit.js"
        };

        var options = Options.Create(settings);
        var processExecutor = new ProcessExecutor(_executorLoggerMock.Object);
        return new GS1ToolkitService(_serviceLoggerMock.Object, processExecutor, options);
    }

    [Fact]
    public async Task TestDigitalLinkSyntaxAsync_WithValidPath_ReturnsTrue()
    {
        // Skip if prerequisites not met
        if (_skipTests)
        {
            return;
        }

        // Arrange
        var service = CreateService();
        var urlPath = "/01/09521234543213/10/LOT123";

        // Act
        var result = await service.TestDigitalLinkSyntaxAsync(urlPath);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task TestDigitalLinkSyntaxAsync_WithInvalidPath_ReturnsFalse()
    {
        // Skip if prerequisites not met
        if (_skipTests)
        {
            return;
        }

        // Arrange
        var service = CreateService();
        var urlPath = "/01/INVALID";

        // Act
        var result = await service.TestDigitalLinkSyntaxAsync(urlPath);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task CompressAndUncompress_RoundTrip_PreservesData()
    {
        // Skip if prerequisites not met
        if (_skipTests)
        {
            return;
        }

        // Arrange
        var service = CreateService();
        var originalLink = "https://id.gs1.org/01/09521234543213/10/LOT123";

        // Act - Compress
        var compressResult = await service.CompressDigitalLinkAsync(originalLink);

        // Assert compress
        Assert.True(compressResult.Success, $"Compression failed: {compressResult.Error}");
        Assert.NotNull(compressResult.Compressed);

        // Act - Uncompress
        var uncompressResult = await service.UncompressDigitalLinkAsync(compressResult.Compressed!);

        // Assert uncompress
        Assert.True(uncompressResult.Success, $"Uncompression failed: {uncompressResult.Error}");
        Assert.NotNull(uncompressResult.Identifiers);

        // Verify data integrity
        var gtin = uncompressResult.Identifiers!.FirstOrDefault()?["01"];
        Assert.Equal("09521234543213", gtin);

        if (uncompressResult.Qualifiers != null && uncompressResult.Qualifiers.Any())
        {
            var lot = uncompressResult.Qualifiers.FirstOrDefault(q => q.ContainsKey("10"))?["10"];
            Assert.Equal("LOT123", lot);
        }
    }

    [Fact]
    public async Task AnalyzeDigitalLinkAsync_WithValidLink_ReturnsStructuredData()
    {
        // Skip if prerequisites not met
        if (_skipTests)
        {
            return;
        }

        // Arrange
        var service = CreateService();
        var digitalLink = "https://id.gs1.org/01/09521234543213/10/LOT123/21/SERIAL456";

        // Act
        var result = await service.AnalyzeDigitalLinkAsync(digitalLink);

        // Assert
        Assert.True(result.Success, $"Analysis failed: {result.Error}");
        Assert.NotNull(result.Identifiers);
        Assert.NotEmpty(result.Identifiers);

        // Verify GTIN identifier
        var gtin = result.Identifiers.FirstOrDefault()?["01"];
        Assert.Equal("09521234543213", gtin);

        // Verify qualifiers
        if (result.Qualifiers != null)
        {
            var hasLot = result.Qualifiers.Any(q => q.ContainsKey("10"));
            var hasSerial = result.Qualifiers.Any(q => q.ContainsKey("21"));
            Assert.True(hasLot || hasSerial, "Should have at least one qualifier");
        }
    }

    [Fact]
    public async Task ProcessExecutor_WithTimeout_HandlesGracefully()
    {
        // Skip if prerequisites not met
        if (_skipTests)
        {
            return;
        }

        // Arrange
        var processExecutor = new ProcessExecutor(_executorLoggerMock.Object);

        // Create a script that sleeps longer than timeout
        var tempScript = Path.Combine(Path.GetTempPath(), "sleep_test.js");
        File.WriteAllText(tempScript, "setTimeout(() => console.log('done'), 10000);");

        try
        {
            // Act
            var result = await processExecutor.ExecuteAsync(
                "node",
                $"\"{tempScript}\"",
                Path.GetTempPath(),
                1000); // 1 second timeout

            // Assert
            Assert.Equal(-1, result.exitCode);
            Assert.Contains("timed out", result.stderr);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempScript))
            {
                File.Delete(tempScript);
            }
        }
    }

    [Fact]
    public async Task ProcessExecutor_WithNonExistentCommand_ReturnsError()
    {
        // Arrange
        var processExecutor = new ProcessExecutor(_executorLoggerMock.Object);

        // Act
        var result = await processExecutor.ExecuteAsync(
            "nonexistent_command_12345",
            "--version",
            Path.GetTempPath(),
            5000);

        // Assert
        Assert.Equal(-1, result.exitCode);
        Assert.Contains("Process execution failed", result.stderr);
    }

    public void Dispose()
    {
        // Cleanup any temporary resources if needed
        GC.SuppressFinalize(this);
    }
}
