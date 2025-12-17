using FluentAssertions;
using GS1Resolver.Shared.Tests.Mocks;

namespace GS1Resolver.Shared.Tests.Mocks;

public class MockGS1ToolkitServiceTests
{
    private readonly MockGS1ToolkitService _service;

    public MockGS1ToolkitServiceTests()
    {
        _service = new MockGS1ToolkitService();
    }

    [Fact]
    public async Task CompressDigitalLink_WithTestCase_ReturnsExpectedCompressedLink()
    {
        // Arrange
        var uncompressedLink = "/01/09506000134376/10/LOT01";

        // Act
        var result = await _service.CompressDigitalLinkAsync(uncompressedLink);

        // Assert
        result.Success.Should().BeTrue();
        result.Compressed.Should().Be("/ARFKk4XB0CDKWcnpq");
    }

    [Fact]
    public async Task CompressDigitalLink_WithDomainPrefix_ReturnsExpectedCompressedLink()
    {
        // Arrange
        var uncompressedLink = "https://localhost:8080/01/09506000134376/10/LOT01";

        // Act
        var result = await _service.CompressDigitalLinkAsync(uncompressedLink);

        // Assert
        result.Success.Should().BeTrue();
        result.Compressed.Should().Be("/ARFKk4XB0CDKWcnpq");
    }

    [Fact]
    public async Task UncompressDigitalLink_WithTestCase_ReturnsExpectedQualifiers()
    {
        // Arrange
        var compressedLink = "/ARFKk4XB0CDKWcnpq";

        // Act
        var result = await _service.UncompressDigitalLinkAsync(compressedLink);

        // Assert
        result.Success.Should().BeTrue();
        result.Identifiers.Should().NotBeNull();
        result.Identifiers.Should().HaveCount(1);
        result.Identifiers![0].Should().ContainKey("01");
        result.Identifiers[0]["01"].Should().Be("09506000134376");

        result.Qualifiers.Should().NotBeNull();
        result.Qualifiers.Should().HaveCount(1);
        result.Qualifiers![0].Should().ContainKey("10");
        result.Qualifiers[0]["10"].Should().Be("LOT01");
    }

    [Fact]
    public async Task CompressDigitalLink_WithOtherInput_ReturnsMockCompressedLink()
    {
        // Arrange
        var uncompressedLink = "/01/09506000134376";

        // Act
        var result = await _service.CompressDigitalLinkAsync(uncompressedLink);

        // Assert
        result.Success.Should().BeTrue();
        result.Compressed.Should().NotBeNullOrEmpty();
        result.Compressed.Should().StartWith("/MOCK");
    }
}
