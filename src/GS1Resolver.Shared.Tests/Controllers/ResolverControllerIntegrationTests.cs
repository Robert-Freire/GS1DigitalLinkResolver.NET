using GS1Resolver.Shared.Configuration;
using GS1Resolver.Shared.Models;
using GS1Resolver.Shared.Repositories;
using GS1Resolver.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using WebResolverService.Controllers;
using Xunit;

namespace GS1Resolver.Shared.Tests.Controllers;

public class ResolverControllerIntegrationTests
{
    private readonly Mock<IResolverRepository> _repositoryMock;
    private readonly Mock<IGS1ToolkitService> _gs1ToolkitMock;
    private readonly Mock<IWebResolverLogicService> _resolverLogicMock;
    private readonly Mock<IContentNegotiationService> _contentNegotiationMock;
    private readonly Mock<ILogger<ResolverController>> _loggerMock;
    private readonly IOptions<FqdnSettings> _fqdnSettings;
    private readonly ResolverController _controller;

    public ResolverControllerIntegrationTests()
    {
        _repositoryMock = new Mock<IResolverRepository>();
        _gs1ToolkitMock = new Mock<IGS1ToolkitService>();
        _resolverLogicMock = new Mock<IWebResolverLogicService>();
        _contentNegotiationMock = new Mock<IContentNegotiationService>();
        _loggerMock = new Mock<ILogger<ResolverController>>();
        _fqdnSettings = Options.Create(new FqdnSettings { DomainName = "example.com" });

        // Setup GS1 Toolkit mocks to always succeed (bypass checksum validation)
        _gs1ToolkitMock
            .Setup(x => x.TestDigitalLinkSyntaxAsync(It.IsAny<string>()))
            .ReturnsAsync(true);

        _gs1ToolkitMock
            .Setup(x => x.CompressDigitalLinkAsync(It.IsAny<string>()))
            .ReturnsAsync(new GS1ToolkitResult
            {
                Success = true,
                Compressed = "/MOCK123"
            });

        _gs1ToolkitMock
            .Setup(x => x.UncompressDigitalLinkAsync(It.IsAny<string>()))
            .ReturnsAsync(new GS1ToolkitResult
            {
                Success = true,
                Identifiers = new List<Dictionary<string, string>>
                {
                    new Dictionary<string, string> { { "01", "00000000000000" } }
                }
            });

        _controller = new ResolverController(
            _repositoryMock.Object,
            _gs1ToolkitMock.Object,
            _resolverLogicMock.Object,
            _contentNegotiationMock.Object,
            _loggerMock.Object,
            _fqdnSettings
        );
    }

    [Fact]
    public void Heartbeat_ReturnsOkWithMessage()
    {
        // Arrange - Setup HttpContext
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        // Act
        var result = _controller.Heartbeat() as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        var value = result.Value as dynamic;
        Assert.NotNull(value);
    }

    [Fact]
    public async Task ResolveIdentifiers_NormalizesGtin13ToGtin14()
    {
        // Arrange
        var aiCode = "01";
        var aiValue = "1234567890123"; // GTIN-13

        // Setup HTTP context
        var httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        _resolverLogicMock.Setup(x => x.ResolveAsync(
                It.Is<string>(s => s == "/01/01234567890123"), // Should be normalized
                null,
                It.IsAny<ResolverRequestContext>()))
            .ReturnsAsync(new ResolverResponse
            {
                StatusCode = 307,
                LocationHeader = "http://example.com/product"
            });

        // Act
        var result = await _controller.ResolveIdentifiers(aiCode, aiValue);

        // Assert
        _resolverLogicMock.Verify(x => x.ResolveAsync(
            "/01/01234567890123",
            null,
            It.IsAny<ResolverRequestContext>()), Times.Once);
    }

    [Fact]
    public async Task ResolveIdentifiers_CompressParameter_ReturnsCompressedLink()
    {
        // Arrange
        var aiCode = "01";
        var aiValue = "09521234543213";

        // Setup HTTP context with query string
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString("?compress=true");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        _gs1ToolkitMock.Setup(x => x.CompressDigitalLinkAsync(It.IsAny<string>()))
            .ReturnsAsync(new GS1ToolkitResult
            {
                Success = true,
                Compressed = "ABCDEF"
            });

        // Act
        var result = await _controller.ResolveIdentifiers(aiCode, aiValue) as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public async Task ResolveWithQualifiers_HandlesQualifiers()
    {
        // Arrange
        var aiCode = "01";
        var aiValue = "09521234543213";
        var qualifiers = "10/LOT123";

        _resolverLogicMock.Setup(x => x.ResolveAsync(
                "/01/09521234543213",
                "/10/LOT123",
                It.IsAny<ResolverRequestContext>()))
            .ReturnsAsync(new ResolverResponse
            {
                StatusCode = 307,
                LocationHeader = "http://example.com/product/lot123"
            });

        // Setup HTTP context
        var httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        // Act
        var result = await _controller.ResolveWithQualifiers(aiCode, aiValue, qualifiers);

        // Assert
        _resolverLogicMock.Verify(x => x.ResolveAsync(
            "/01/09521234543213",
            "/10/LOT123",
            It.IsAny<ResolverRequestContext>()), Times.Once);
    }

    [Fact]
    public async Task ResolveCompressed_UncompressesAndResolves()
    {
        // Arrange
        var compressedLink = "ABCDEF";

        _gs1ToolkitMock.Setup(x => x.UncompressDigitalLinkAsync(compressedLink))
            .ReturnsAsync(new GS1ToolkitResult
            {
                Success = true,
                Identifiers = new List<Dictionary<string, string>>
                {
                    new Dictionary<string, string> { { "01", "09521234543213" } }
                }
            });

        _resolverLogicMock.Setup(x => x.ResolveAsync(
                "/01/09521234543213",
                null,
                It.IsAny<ResolverRequestContext>()))
            .ReturnsAsync(new ResolverResponse
            {
                StatusCode = 307,
                LocationHeader = "http://example.com/product"
            });

        // Setup HTTP context
        var httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        // Act
        var result = await _controller.ResolveCompressed(compressedLink);

        // Assert
        _gs1ToolkitMock.Verify(x => x.UncompressDigitalLinkAsync(compressedLink), Times.Once);
    }

    [Fact]
    public async Task WellKnownResolver_ReturnsConfiguration()
    {
        // Arrange - Setup HttpContext with Request properties
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("example.com");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        // Act
        var result = await _controller.WellKnownResolver();

        // Assert
        Assert.NotNull(result);
        // Result can be either OkObjectResult or ContentResult
        var statusCode = result switch
        {
            OkObjectResult okResult => okResult.StatusCode,
            ContentResult contentResult => 200,
            _ => 0
        };
        Assert.Equal(200, statusCode);
    }

    [Fact]
    public async Task ResolveIdentifiers_AcceptHeader_PassedToService()
    {
        // Arrange
        var aiCode = "01";
        var aiValue = "09521234543213";

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("example.com");
        httpContext.Request.Headers["Accept"] = "application/json";
        httpContext.Request.Headers["Accept-Language"] = "en-US,en;q=0.9";
        httpContext.Items["MediaTypesList"] = new List<string> { "application/json" };
        httpContext.Items["AcceptLanguageList"] = new List<string> { "en-US", "en" };

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        _resolverLogicMock.Setup(x => x.ResolveAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ResolverRequestContext>()))
            .ReturnsAsync(new ResolverResponse
            {
                StatusCode = 200,
                Data = new { }
            });

        // Act
        await _controller.ResolveIdentifiers(aiCode, aiValue);

        // Assert
        _resolverLogicMock.Verify(x => x.ResolveAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<ResolverRequestContext>()), Times.Once);
    }
}
