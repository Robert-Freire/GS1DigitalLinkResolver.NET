using DataEntryService.Controllers;
using GS1Resolver.Shared.Exceptions;
using GS1Resolver.Shared.Models;
using GS1Resolver.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GS1Resolver.Shared.Tests.Controllers;

/// <summary>
/// Unit tests for DataEntryController.
/// Uses mocked dependencies to test controller behavior in isolation.
/// Does not validate GTIN checksums - uses mocks that always succeed.
/// </summary>
public class DataEntryControllerIntegrationTests
{
    private readonly Mock<IDataEntryLogicService> _logicServiceMock;
    private readonly Mock<ILogger<DataEntryController>> _loggerMock;
    private readonly DataEntryController _controller;

    public DataEntryControllerIntegrationTests()
    {
        _logicServiceMock = new Mock<IDataEntryLogicService>();
        _loggerMock = new Mock<ILogger<DataEntryController>>();

        _controller = new DataEntryController(
            _logicServiceMock.Object,
            _loggerMock.Object
        );

        // Setup default HttpContext
        var httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task CreateSingleAsync_ShouldAcceptSingleDocument()
    {
        // Arrange
        var document = new DataEntryV3Document
        {
            Anchor = "/01/09506000134376",
            ItemDescription = "Test Product",
            Links = new List<LinkV3>
            {
                new LinkV3
                {
                    Linktype = "gs1:pip",
                    Href = "https://example.com/product",
                    Title = "Product Information"
                }
            }
        };

        var expectedResult = new List<CreateResult>
        {
            new CreateResult
            {
                Id = "01_09506000134376",
                Status = 201,
                Message = "Created"
            }
        };

        _logicServiceMock
            .Setup(x => x.CreateDocumentAsync(It.IsAny<List<DataEntryV3Document>>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.CreateSingleAsync(document);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status201Created, objectResult.StatusCode);

        var returnedResults = Assert.IsType<List<CreateResult>>(objectResult.Value);
        Assert.Single(returnedResults);
        Assert.Equal("01_09506000134376", returnedResults[0].Id);
    }

    [Fact]
    public async Task CreateAsync_ShouldAcceptArrayOfDocuments()
    {
        // Arrange
        var documents = new List<DataEntryV3Document>
        {
            new DataEntryV3Document
            {
                Anchor = "/01/09506000134376",
                Links = new List<LinkV3>
                {
                    new LinkV3 { Linktype = "gs1:pip", Href = "https://example.com/1", Title = "Link 1" }
                }
            },
            new DataEntryV3Document
            {
                Anchor = "/01/09506000134377",
                Links = new List<LinkV3>
                {
                    new LinkV3 { Linktype = "gs1:pip", Href = "https://example.com/2", Title = "Link 2" }
                }
            }
        };

        var expectedResults = new List<CreateResult>
        {
            new CreateResult { Id = "01_09506000134376", Status = 201, Message = "Created" },
            new CreateResult { Id = "01_09506000134377", Status = 201, Message = "Created" }
        };

        _logicServiceMock
            .Setup(x => x.CreateDocumentAsync(It.IsAny<List<DataEntryV3Document>>()))
            .ReturnsAsync(expectedResults);

        // Act
        var result = await _controller.CreateAsync(documents);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status201Created, objectResult.StatusCode);

        var returnedResults = Assert.IsType<List<CreateResult>>(objectResult.Value);
        Assert.Equal(2, returnedResults.Count);
    }

    [Fact]
    public async Task IndexAsync_ShouldReturnListOfAnchors()
    {
        // Arrange
        var expectedAnchors = new List<string>
        {
            "/01/09506000134376",
            "/01/09506000134377",
            "/01/09506000134376/21/12345"
        };

        _logicServiceMock
            .Setup(x => x.ReadIndexAsync())
            .ReturnsAsync(expectedAnchors);

        // Act
        var result = await _controller.IndexAsync();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(200, okResult.StatusCode);
        var returnedAnchors = Assert.IsType<List<string>>(okResult.Value);
        Assert.Equal(3, returnedAnchors.Count);
    }

    [Fact]
    public async Task Get_ShouldReturnV3Documents()
    {
        // Arrange
        var expectedDocuments = new List<DataEntryV3Document>
        {
            new DataEntryV3Document
            {
                Anchor = "/01/09506000134376",
                ItemDescription = "Test Product",
                Links = new List<LinkV3>
                {
                    new LinkV3
                    {
                        Linktype = "gs1:pip",
                        Href = "https://example.com/product",
                        Title = "Product Info"
                    }
                }
            }
        };

        _logicServiceMock
            .Setup(x => x.ReadDocumentAsync("01_09506000134376"))
            .ReturnsAsync(expectedDocuments);

        // Act
        var result = await _controller.Get("01", "09506000134376");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(200, okResult.StatusCode);
        var returnedDocs = Assert.IsType<List<DataEntryV3Document>>(okResult.Value);
        Assert.Single(returnedDocs);
    }

    [Fact]
    public async Task GetByFullPath_ShouldHandleQualifiers()
    {
        // Arrange
        var expectedDocuments = new List<DataEntryV3Document>
        {
            new DataEntryV3Document
            {
                Anchor = "/01/09506000134376/21/12345",
                Links = new List<LinkV3>()
            }
        };

        _logicServiceMock
            .Setup(x => x.ConvertPathToDocumentId("/01/09506000134376/21/12345"))
            .Returns("01_09506000134376_21_12345");

        _logicServiceMock
            .Setup(x => x.ReadDocumentAsync("01_09506000134376_21_12345"))
            .ReturnsAsync(expectedDocuments);

        // Act
        var result = await _controller.GetByFullPath("01", "09506000134376", "21/12345");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(200, okResult.StatusCode);
        var returnedDocs = Assert.IsType<List<DataEntryV3Document>>(okResult.Value);
        Assert.Single(returnedDocs);
    }

    [Fact]
    public async Task Update_ShouldUpsertDocument()
    {
        // Arrange
        var documents = new List<DataEntryV3Document>
        {
            new DataEntryV3Document
            {
                Anchor = "/01/09506000134376",
                Links = new List<LinkV3>
                {
                    new LinkV3 { Linktype = "gs1:pip", Href = "https://example.com/updated", Title = "Updated" }
                }
            }
        };

        var expectedResults = new List<CreateResult>
        {
            new CreateResult { Id = "01_09506000134376", Status = 200, Message = "Updated" }
        };

        _logicServiceMock
            .Setup(x => x.CreateDocumentAsync(It.IsAny<List<DataEntryV3Document>>()))
            .ReturnsAsync(expectedResults);

        // Act
        var result = await _controller.Update("01", "09506000134376", documents);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        var returnedResults = Assert.IsType<List<CreateResult>>(okResult.Value);
        Assert.Single(returnedResults);
    }

    [Fact]
    public async Task Delete_ShouldRemoveDocument()
    {
        // Arrange
        _logicServiceMock
            .Setup(x => x.DeleteDocumentAsync("01_09506000134376"))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Delete("01", "09506000134376");

        // Assert
        var noContentResult = Assert.IsType<NoContentResult>(result);
        Assert.Equal(204, noContentResult.StatusCode);
        _logicServiceMock.Verify(x => x.DeleteDocumentAsync("01_09506000134376"), Times.Once);
    }

    [Fact]
    public async Task Delete_ShouldReturn404WhenNotFound()
    {
        // Arrange
        _logicServiceMock
            .Setup(x => x.DeleteDocumentAsync("01_99999999999999"))
            .ThrowsAsync(new NotFoundException("Document not found"));

        // Act
        var result = await _controller.Delete("01", "99999999999999");

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFoundResult.StatusCode);
    }

    [Fact]
    public async Task MigrateV2_ShouldConvertV2ToV3()
    {
        // Arrange
        var v2Doc = new DataEntryV2Document
        {
            IdentificationKeyType = "01",
            IdentificationKey = "09506000134376",
            ItemDescription = "Test Product",
            QualifierPath = "/21/12345",
            Responses = new List<ResponseItemV2>
            {
                new ResponseItemV2
                {
                    LinkType = "pip",
                    TargetUrl = "https://example.com",
                    LinkTitle = "Product Info",
                    DefaultLinkType = true,
                    Active = true
                }
            }
        };

        var expectedV3Docs = new List<DataEntryV3Document>
        {
            new DataEntryV3Document
            {
                Anchor = "/01/09506000134376/21/12345",
                ItemDescription = "Test Product",
                DefaultLinktype = "gs1:pip",
                Links = new List<LinkV3>
                {
                    new LinkV3
                    {
                        Linktype = "gs1:pip",
                        Href = "https://example.com",
                        Title = "Product Info"
                    }
                }
            }
        };

        _logicServiceMock
            .Setup(x => x.MigrateV2Async(It.IsAny<DataEntryV2Document>()))
            .ReturnsAsync(expectedV3Docs);

        // Act
        var result = await _controller.MigrateV2(v2Doc);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(200, okResult.StatusCode);
        var returnedDocs = Assert.IsType<List<DataEntryV3Document>>(okResult.Value);
        Assert.Single(returnedDocs);
    }

    [Fact]
    public async Task CreateAsync_ShouldReturn400WhenDocumentsNull()
    {
        // Act
        var result = await _controller.CreateAsync(null);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(400, badRequestResult.StatusCode);
    }

    [Fact]
    public async Task CreateAsync_ShouldReturn400WhenDocumentsEmpty()
    {
        // Act
        var result = await _controller.CreateAsync(new List<DataEntryV3Document>());

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(400, badRequestResult.StatusCode);
    }
}
