using GS1Resolver.Shared.Configuration;
using GS1Resolver.Shared.Models;
using GS1Resolver.Shared.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace GS1Resolver.Shared.Tests.Services;

/// <summary>
/// Unit tests for GS1ToolkitService using mocked IProcessExecutor.
/// Tests all public methods with various success and failure scenarios.
/// </summary>
public class GS1ToolkitServiceTests
{
    private readonly Mock<ILogger<GS1ToolkitService>> _loggerMock;
    private readonly Mock<IProcessExecutor> _processExecutorMock;
    private readonly GS1ToolkitSettings _settings;
    private readonly string _testToolkitScriptPath;

    public GS1ToolkitServiceTests()
    {
        _loggerMock = new Mock<ILogger<GS1ToolkitService>>();
        _processExecutorMock = new Mock<IProcessExecutor>();

        // Create temporary test script files
        var tempDir = Path.Combine(Path.GetTempPath(), "gs1_toolkit_tests");
        Directory.CreateDirectory(tempDir);
        _testToolkitScriptPath = Path.Combine(tempDir, "callGS1toolkit.js");

        // Create empty script file so File.Exists checks pass
        File.WriteAllText(_testToolkitScriptPath, "// test toolkit script");

        _settings = new GS1ToolkitSettings
        {
            NodePath = "node",
            ToolkitPath = tempDir,
            ToolkitScriptName = "callGS1toolkit.js"
        };
    }

    private GS1ToolkitService CreateService()
    {
        var options = Options.Create(_settings);
        return new GS1ToolkitService(_loggerMock.Object, _processExecutorMock.Object, options);
    }

    #region TestDigitalLinkSyntaxAsync Tests

    [Fact]
    public async Task TestDigitalLinkSyntaxAsync_WithValidPath_ReturnsTrue()
    {
        // Arrange
        var service = CreateService();
        var urlPath = "/01/09521234543213/10/LOT123";
        var toolkitResponse = @"{""identifiers"":[{""01"":""09521234543213""}],""qualifiers"":[{""10"":""LOT123""}],""SUCCESS"":true}";

        _processExecutorMock
            .Setup(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.Is<string>(args => args.Contains("callGS1toolkit.js") && args.Contains(urlPath)),
                It.IsAny<string>(),
                It.IsAny<int>()))
            .ReturnsAsync((0, toolkitResponse, string.Empty));

        // Act
        var result = await service.TestDigitalLinkSyntaxAsync(urlPath);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task TestDigitalLinkSyntaxAsync_WithNullPath_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.TestDigitalLinkSyntaxAsync(null!);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task TestDigitalLinkSyntaxAsync_WithEmptyPath_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.TestDigitalLinkSyntaxAsync(string.Empty);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task TestDigitalLinkSyntaxAsync_WhenToolkitFails_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();
        var urlPath = "/01/INVALID/10/LOT";

        _processExecutorMock
            .Setup(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>()))
            .ReturnsAsync((1, string.Empty, "Invalid AI"));

        // Act
        var result = await service.TestDigitalLinkSyntaxAsync(urlPath);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task TestDigitalLinkSyntaxAsync_WhenNoIdentifiers_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();
        var urlPath = "/01/09521234543213";
        var toolkitResponse = @"{""identifiers"":[],""qualifiers"":[],""SUCCESS"":true}";

        _processExecutorMock
            .Setup(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>()))
            .ReturnsAsync((0, toolkitResponse, string.Empty));

        // Act
        var result = await service.TestDigitalLinkSyntaxAsync(urlPath);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region UncompressDigitalLinkAsync Tests

    [Fact]
    public async Task UncompressDigitalLinkAsync_WithValidInput_ReturnsSuccess()
    {
        // Arrange
        var service = CreateService();
        var compressedLink = "/AQnO2IRCICDKWcnpqQs6QiOu2_A";
        var jsonResponse = @"{
            ""SUCCESS"": true,
            ""identifiers"": [{""01"": ""09521234543213""}],
            ""qualifiers"": [{""10"": ""LOT123""}, {""21"": ""SERIAL456""}]
        }";

        _processExecutorMock
            .Setup(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>()))
            .ReturnsAsync((0, jsonResponse, string.Empty));

        // Act
        var result = await service.UncompressDigitalLinkAsync(compressedLink);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Identifiers);
        Assert.Single(result.Identifiers);
        Assert.Equal("09521234543213", result.Identifiers[0]["01"]);
        Assert.NotNull(result.Qualifiers);
        Assert.Equal(2, result.Qualifiers.Count);

        _processExecutorMock.Verify(x => x.ExecuteAsync(
            "node",
            It.Is<string>(s => s.Contains("callGS1toolkit.js") && s.Contains(compressedLink) && s.Contains("uncompress")),
            _settings.ToolkitPath,
            30000), Times.Once);
    }

    [Fact]
    public async Task UncompressDigitalLinkAsync_WithNullInput_ReturnsError()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.UncompressDigitalLinkAsync(null!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Compressed link cannot be null or empty", result.Error);
    }

    [Fact]
    public async Task UncompressDigitalLinkAsync_WhenProcessFails_ReturnsError()
    {
        // Arrange
        var service = CreateService();
        var compressedLink = "/INVALID";
        var errorMessage = "Invalid compressed format";

        _processExecutorMock
            .Setup(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>()))
            .ReturnsAsync((1, string.Empty, errorMessage));

        // Act
        var result = await service.UncompressDigitalLinkAsync(compressedLink);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(errorMessage, result.Error);
    }

    [Fact]
    public async Task UncompressDigitalLinkAsync_WithInvalidJson_ReturnsError()
    {
        // Arrange
        var service = CreateService();
        var compressedLink = "/AQnO2IRCICDKWcnpqQs6QiOu2_A";

        _processExecutorMock
            .Setup(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>()))
            .ReturnsAsync((0, "INVALID JSON", string.Empty));

        // Act
        var result = await service.UncompressDigitalLinkAsync(compressedLink);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("JSON parsing error:", result.Error);
    }

    #endregion

    #region CompressDigitalLinkAsync Tests

    [Fact]
    public async Task CompressDigitalLinkAsync_WithValidInput_ReturnsSuccess()
    {
        // Arrange
        var service = CreateService();
        var uncompressedLink = "https://id.gs1.org/01/09521234543213/10/LOT123";
        var expectedCompressed = "/AQnO2IRCICDKWcnpqQs6QiOu2_A";
        var jsonResponse = $@"{{
            ""SUCCESS"": true,
            ""COMPRESSED"": ""{expectedCompressed}""
        }}";

        _processExecutorMock
            .Setup(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>()))
            .ReturnsAsync((0, jsonResponse, string.Empty));

        // Act
        var result = await service.CompressDigitalLinkAsync(uncompressedLink);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(expectedCompressed, result.Compressed);

        _processExecutorMock.Verify(x => x.ExecuteAsync(
            "node",
            It.Is<string>(s => s.Contains("callGS1toolkit.js") && s.Contains(uncompressedLink) && s.Contains("compress")),
            _settings.ToolkitPath,
            30000), Times.Once);
    }

    [Fact]
    public async Task CompressDigitalLinkAsync_WithNullInput_ReturnsError()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.CompressDigitalLinkAsync(null!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Uncompressed link cannot be null or empty", result.Error);
    }

    [Fact]
    public async Task CompressDigitalLinkAsync_WhenProcessFails_ReturnsError()
    {
        // Arrange
        var service = CreateService();
        var uncompressedLink = "https://id.gs1.org/invalid";
        var errorMessage = "Invalid URI format";

        _processExecutorMock
            .Setup(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>()))
            .ReturnsAsync((1, string.Empty, errorMessage));

        // Act
        var result = await service.CompressDigitalLinkAsync(uncompressedLink);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(errorMessage, result.Error);
    }

    #endregion

    #region AnalyzeDigitalLinkAsync Tests

    [Fact]
    public async Task AnalyzeDigitalLinkAsync_WithValidInput_ReturnsSuccess()
    {
        // Arrange
        var service = CreateService();
        var digitalLink = "https://id.gs1.org/01/09521234543213/10/LOT123";
        var jsonResponse = @"{
            ""SUCCESS"": true,
            ""identifiers"": [{""01"": ""09521234543213""}],
            ""qualifiers"": [{""10"": ""LOT123""}],
            ""dataAttributes"": []
        }";

        _processExecutorMock
            .Setup(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>()))
            .ReturnsAsync((0, jsonResponse, string.Empty));

        // Act
        var result = await service.AnalyzeDigitalLinkAsync(digitalLink);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Identifiers);
        Assert.Single(result.Identifiers);
        Assert.Equal("09521234543213", result.Identifiers[0]["01"]);

        _processExecutorMock.Verify(x => x.ExecuteAsync(
            "node",
            It.Is<string>(s => s.Contains("callGS1toolkit.js") && s.Contains(digitalLink) && !s.Contains("compress") && !s.Contains("uncompress")),
            _settings.ToolkitPath,
            30000), Times.Once);
    }

    [Fact]
    public async Task AnalyzeDigitalLinkAsync_WithNullInput_ReturnsError()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.AnalyzeDigitalLinkAsync(null!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Digital link cannot be null or empty", result.Error);
    }

    [Fact]
    public async Task AnalyzeDigitalLinkAsync_WhenProcessFails_ReturnsError()
    {
        // Arrange
        var service = CreateService();
        var digitalLink = "https://invalid.url";
        var errorMessage = "Analysis failed";

        _processExecutorMock
            .Setup(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>()))
            .ReturnsAsync((1, string.Empty, errorMessage));

        // Act
        var result = await service.AnalyzeDigitalLinkAsync(digitalLink);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(errorMessage, result.Error);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithMissingToolkitScript_LogsWarning()
    {
        // Arrange
        var settings = new GS1ToolkitSettings
        {
            NodePath = "node",
            ToolkitPath = Path.GetTempPath(),
            ToolkitScriptName = "nonexistent_toolkit.js"
        };
        var options = Options.Create(settings);

        // Act
        var service = new GS1ToolkitService(_loggerMock.Object, _processExecutorMock.Object, options);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("toolkit script not found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}
