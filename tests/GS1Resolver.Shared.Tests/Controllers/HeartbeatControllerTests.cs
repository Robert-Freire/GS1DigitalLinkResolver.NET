using DataEntryService.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GS1Resolver.Shared.Tests.Controllers;

/// <summary>
/// Unit tests for DataEntry HeartbeatController.
/// Tests health check endpoint behavior.
/// </summary>
public class HeartbeatControllerTests
{
    private readonly Mock<ILogger<HeartbeatController>> _loggerMock;
    private readonly HeartbeatController _controller;

    public HeartbeatControllerTests()
    {
        _loggerMock = new Mock<ILogger<HeartbeatController>>();
        _controller = new HeartbeatController(_loggerMock.Object);

        // Setup default HttpContext
        var httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public void Get_ReturnsOkWithMessage()
    {
        // Act
        var result = _controller.Get() as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        var value = result.Value as dynamic;
        Assert.NotNull(value);
    }

    [Fact]
    public void Get_ReturnsJsonObject()
    {
        // Act
        var result = _controller.Get() as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Value);
        var valueType = result.Value.GetType();
        Assert.True(valueType.IsAnonymousType() || valueType.IsClass);
    }
}

/// <summary>
/// Extension methods for type checking in tests.
/// </summary>
public static class TypeExtensions
{
    public static bool IsAnonymousType(this Type type)
    {
        return type.Name.Contains("AnonymousType")
               && (type.Name.StartsWith("<>") || type.Name.StartsWith("VB$"))
               && type.GetCustomAttributes(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), true).Length > 0;
    }
}
