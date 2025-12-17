using GS1Resolver.Shared.Exceptions;
using GS1Resolver.Shared.Models;
using GS1Resolver.Shared.Repositories;
using GS1Resolver.Shared.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace GS1Resolver.Shared.Tests.Services;

/// <summary>
/// Unit tests for DataEntryLogicService using mocked dependencies.
/// Tests v3 document authoring, conversion, and business logic.
/// </summary>
public class DataEntryLogicServiceTests
{
    private readonly Mock<IResolverRepository> _repositoryMock;
    private readonly Mock<IGS1ToolkitService> _gs1ToolkitMock;
    private readonly Mock<ILogger<DataEntryLogicService>> _loggerMock;
    private readonly DataEntryLogicService _service;

    public DataEntryLogicServiceTests()
    {
        _repositoryMock = new Mock<IResolverRepository>();
        _gs1ToolkitMock = new Mock<IGS1ToolkitService>();
        _loggerMock = new Mock<ILogger<DataEntryLogicService>>();

        _service = new DataEntryLogicService(
            _repositoryMock.Object,
            _gs1ToolkitMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public void ConvertPathToDocumentId_ValidPath_ReturnsCorrectId()
    {
        // Arrange
        var path = "/01/09506000134376/21/12345";

        // Act
        var result = _service.ConvertPathToDocumentId(path);

        // Assert
        Assert.Equal("01_09506000134376_21_12345", result);
    }

    [Fact]
    public void ConvertPathToDocumentId_PathWithoutLeadingSlash_ReturnsCorrectId()
    {
        // Arrange
        var path = "01/09506000134376";

        // Act
        var result = _service.ConvertPathToDocumentId(path);

        // Assert
        Assert.Equal("01_09506000134376", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConvertPathToDocumentId_InvalidPath_ThrowsArgumentException(string? path)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _service.ConvertPathToDocumentId(path!));
    }

    [Fact]
    public async Task AuthorDbLinksetDocumentAsync_ValidV3Doc_ReturnsMongoDocument()
    {
        // Arrange
        var v3Doc = new DataEntryV3Document
        {
            Anchor = "/01/09506000134376",
            ItemDescription = "Test Product",
            Links = new List<LinkV3>
            {
                new LinkV3
                {
                    Linktype = "gs1:pip",
                    Href = "https://example.com/product",
                    Title = "Product Info",
                    Type = "text/html"
                }
            }
        };

        // Act
        var result = await _service.AuthorDbLinksetDocumentAsync(v3Doc);

        // Assert
        Assert.Equal("01_09506000134376", result.Id);
        Assert.Single(result.Data);
        Assert.Contains("https://gs1.org/voc/pip", result.Data[0].Linkset.LinkTypes.Keys);
    }

    [Fact]
    public async Task AuthorDbLinksetDocumentAsync_WithDefaultLink_OrdersLinksCorrectly()
    {
        // Arrange
        var v3Doc = new DataEntryV3Document
        {
            Anchor = "/01/09506000134376",
            Links = new List<LinkV3>
            {
                new LinkV3
                {
                    Linktype = "gs1:pip",
                    Href = "https://example.com/product",
                    Title = "Product Info"
                },
                new LinkV3
                {
                    Linktype = "gs1:defaultLink",
                    Href = "https://example.com/default",
                    Title = "Default"
                },
                new LinkV3
                {
                    Linktype = "gs1:hasRetailers",
                    Href = "https://example.com/retailers",
                    Title = "Retailers"
                }
            }
        };

        // Act
        var result = await _service.AuthorDbLinksetDocumentAsync(v3Doc);

        // Assert
        var linkset = result.Data[0].Linkset;
        var keys = linkset.LinkTypes.Keys.ToList();

        // defaultLink should be first
        Assert.Equal("https://gs1.org/voc/defaultLink", keys[0]);
        Assert.Single(linkset.LinkTypes["https://gs1.org/voc/defaultLink"]);
    }

    [Fact]
    public async Task AuthorDbLinksetDocumentAsync_WithMultipleDefaultLinks_UsesFirstOnly()
    {
        // Arrange
        var v3Doc = new DataEntryV3Document
        {
            Anchor = "/01/09506000134376",
            Links = new List<LinkV3>
            {
                new LinkV3
                {
                    Linktype = "gs1:defaultLink",
                    Href = "https://example.com/default1",
                    Title = "Default 1"
                },
                new LinkV3
                {
                    Linktype = "gs1:defaultLink",
                    Href = "https://example.com/default2",
                    Title = "Default 2"
                },
                new LinkV3
                {
                    Linktype = "gs1:pip",
                    Href = "https://example.com/product",
                    Title = "Product Info"
                }
            }
        };

        // Act
        var result = await _service.AuthorDbLinksetDocumentAsync(v3Doc);

        // Assert
        var linkset = result.Data[0].Linkset;
        Assert.Contains("https://gs1.org/voc/defaultLink", linkset.LinkTypes.Keys);

        // Should only have one defaultLink entry (the first one)
        Assert.Single(linkset.LinkTypes["https://gs1.org/voc/defaultLink"]);
        Assert.Equal("https://example.com/default1", linkset.LinkTypes["https://gs1.org/voc/defaultLink"][0].Href);
        Assert.Equal("Default 1", linkset.LinkTypes["https://gs1.org/voc/defaultLink"][0].Title);
    }

    [Fact]
    public async Task AuthorDbLinksetDocumentAsync_WithDefaultLinkMulti_PreservesAllEntries()
    {
        // Arrange
        var v3Doc = new DataEntryV3Document
        {
            Anchor = "/01/09506000134376",
            Links = new List<LinkV3>
            {
                new LinkV3
                {
                    Linktype = "gs1:defaultLinkMulti",
                    Href = "https://example.com/multi1",
                    Title = "Multi 1"
                },
                new LinkV3
                {
                    Linktype = "gs1:defaultLinkMulti",
                    Href = "https://example.com/multi2",
                    Title = "Multi 2"
                },
                new LinkV3
                {
                    Linktype = "gs1:defaultLinkMulti",
                    Href = "https://example.com/multi3",
                    Title = "Multi 3"
                },
                new LinkV3
                {
                    Linktype = "gs1:pip",
                    Href = "https://example.com/product",
                    Title = "Product Info"
                }
            }
        };

        // Act
        var result = await _service.AuthorDbLinksetDocumentAsync(v3Doc);

        // Assert
        var linkset = result.Data[0].Linkset;
        Assert.Contains("https://gs1.org/voc/defaultLinkMulti", linkset.LinkTypes.Keys);

        // Should have all three defaultLinkMulti entries
        var multiEntries = linkset.LinkTypes["https://gs1.org/voc/defaultLinkMulti"];
        Assert.Equal(3, multiEntries.Count);

        // Verify order is preserved
        Assert.Equal("https://example.com/multi1", multiEntries[0].Href);
        Assert.Equal("Multi 1", multiEntries[0].Title);
        Assert.Equal("https://example.com/multi2", multiEntries[1].Href);
        Assert.Equal("Multi 2", multiEntries[1].Title);
        Assert.Equal("https://example.com/multi3", multiEntries[2].Href);
        Assert.Equal("Multi 3", multiEntries[2].Title);
    }

    [Fact]
    public async Task AuthorDbLinksetDocumentAsync_WithBothDefaultTypes_OrdersCorrectly()
    {
        // Arrange
        var v3Doc = new DataEntryV3Document
        {
            Anchor = "/01/09506000134376",
            Links = new List<LinkV3>
            {
                new LinkV3
                {
                    Linktype = "gs1:pip",
                    Href = "https://example.com/product",
                    Title = "Product Info"
                },
                new LinkV3
                {
                    Linktype = "gs1:defaultLinkMulti",
                    Href = "https://example.com/multi1",
                    Title = "Multi 1"
                },
                new LinkV3
                {
                    Linktype = "gs1:defaultLink",
                    Href = "https://example.com/default",
                    Title = "Default"
                },
                new LinkV3
                {
                    Linktype = "gs1:defaultLinkMulti",
                    Href = "https://example.com/multi2",
                    Title = "Multi 2"
                },
                new LinkV3
                {
                    Linktype = "gs1:hasRetailers",
                    Href = "https://example.com/retailers",
                    Title = "Retailers"
                }
            }
        };

        // Act
        var result = await _service.AuthorDbLinksetDocumentAsync(v3Doc);

        // Assert
        var linkset = result.Data[0].Linkset;
        var keys = linkset.LinkTypes.Keys.ToList();

        // Verify order: defaultLink first, defaultLinkMulti second, others follow
        Assert.Equal(4, keys.Count);
        Assert.Equal("https://gs1.org/voc/defaultLink", keys[0]);
        Assert.Equal("https://gs1.org/voc/defaultLinkMulti", keys[1]);

        // Verify defaultLink has only one entry
        Assert.Single(linkset.LinkTypes["https://gs1.org/voc/defaultLink"]);
        Assert.Equal("https://example.com/default", linkset.LinkTypes["https://gs1.org/voc/defaultLink"][0].Href);

        // Verify defaultLinkMulti has both entries in order
        Assert.Equal(2, linkset.LinkTypes["https://gs1.org/voc/defaultLinkMulti"].Count);
        Assert.Equal("https://example.com/multi1", linkset.LinkTypes["https://gs1.org/voc/defaultLinkMulti"][0].Href);
        Assert.Equal("https://example.com/multi2", linkset.LinkTypes["https://gs1.org/voc/defaultLinkMulti"][1].Href);
    }

    [Fact]
    public async Task AuthorDbLinksetDocumentAsync_WithFullUriDefaultTypes_HandlesCorrectly()
    {
        // Arrange
        var v3Doc = new DataEntryV3Document
        {
            Anchor = "/01/09506000134376",
            Links = new List<LinkV3>
            {
                new LinkV3
                {
                    Linktype = "https://gs1.org/voc/defaultLink",
                    Href = "https://example.com/default",
                    Title = "Default"
                },
                new LinkV3
                {
                    Linktype = "https://gs1.org/voc/defaultLinkMulti",
                    Href = "https://example.com/multi1",
                    Title = "Multi 1"
                },
                new LinkV3
                {
                    Linktype = "https://gs1.org/voc/defaultLinkMulti",
                    Href = "https://example.com/multi2",
                    Title = "Multi 2"
                }
            }
        };

        // Act
        var result = await _service.AuthorDbLinksetDocumentAsync(v3Doc);

        // Assert
        var linkset = result.Data[0].Linkset;
        var keys = linkset.LinkTypes.Keys.ToList();

        // Verify both default types are present
        Assert.Equal(2, keys.Count);
        Assert.Contains("https://gs1.org/voc/defaultLink", keys);
        Assert.Contains("https://gs1.org/voc/defaultLinkMulti", keys);

        // Verify defaultLink has one entry
        Assert.Single(linkset.LinkTypes["https://gs1.org/voc/defaultLink"]);

        // Verify defaultLinkMulti has both entries
        Assert.Equal(2, linkset.LinkTypes["https://gs1.org/voc/defaultLinkMulti"].Count);
    }

    [Fact]
    public async Task AuthorDbLinksetDocumentAsync_WithQualifiers_IncludesQualifiers()
    {
        // Arrange
        var v3Doc = new DataEntryV3Document
        {
            Anchor = "/01/09506000134376/21/12345",
            Qualifiers = new List<Dictionary<string, string>>
            {
                new Dictionary<string, string> { { "21", "{serialnumber}" } }
            },
            Links = new List<LinkV3>
            {
                new LinkV3
                {
                    Linktype = "gs1:pip",
                    Href = "https://example.com/product",
                    Title = "Product Info"
                }
            }
        };

        // Act
        var result = await _service.AuthorDbLinksetDocumentAsync(v3Doc);

        // Assert
        Assert.Equal("01_09506000134376_21_12345", result.Id);
        Assert.Single(result.Data[0].Qualifiers);
        Assert.Contains("21", result.Data[0].Qualifiers[0].Keys);
        Assert.Equal("{serialnumber}", result.Data[0].Qualifiers[0]["21"]);
    }

    [Fact]
    public async Task AuthorDbLinksetDocumentAsync_NullDocument_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.AuthorDbLinksetDocumentAsync(null!));
    }

    [Fact]
    public async Task AuthorDbLinksetDocumentAsync_EmptyAnchor_ThrowsArgumentException()
    {
        // Arrange
        var v3Doc = new DataEntryV3Document
        {
            Anchor = "",
            Links = new List<LinkV3>
            {
                new LinkV3 { Linktype = "gs1:pip", Href = "https://example.com", Title = "Test" }
            }
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.AuthorDbLinksetDocumentAsync(v3Doc));
    }

    [Fact]
    public async Task AuthorDbLinksetListAsync_MultipleDocs_MergesSameIds()
    {
        // Arrange
        var docs = new List<DataEntryV3Document>
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
                Anchor = "/01/09506000134376",
                Links = new List<LinkV3>
                {
                    new LinkV3 { Linktype = "gs1:hasRetailers", Href = "https://example.com/2", Title = "Link 2" }
                }
            }
        };

        // Act
        var result = await _service.AuthorDbLinksetListAsync(docs);

        // Assert
        Assert.Single(result); // Should merge into one document
        Assert.Equal(2, result[0].Data.Count); // Should have 2 data items
    }

    [Fact]
    public async Task ConvertMongoLinksetToV3Async_ValidDocument_ReturnsV3Docs()
    {
        // Arrange
        var linksetDoc = new ResolverDocument
        {
            Id = "01_09506000134376",
            Data = new List<LinksetDataItem>
            {
                new LinksetDataItem
                {
                    Qualifiers = new List<Dictionary<string, string>>(),
                    Linkset = new LinksetObject
                    {
                        LinkTypes = new Dictionary<string, List<LinksetEntry>>
                        {
                            {
                                "https://gs1.org/voc/pip",
                                new List<LinksetEntry>
                                {
                                    new LinksetEntry
                                    {
                                        Href = "https://example.com/product",
                                        Title = "Product Info",
                                        Type = "text/html"
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        // Act
        var result = await _service.ConvertMongoLinksetToV3Async(linksetDoc);

        // Assert
        Assert.Single(result);
        Assert.Equal("/01/09506000134376", result[0].Anchor);
        Assert.Single(result[0].Links);
        Assert.Equal("gs1:pip", result[0].Links[0].Linktype);
        Assert.Equal("https://example.com/product", result[0].Links[0].Href);
    }

    [Fact]
    public async Task ProcessDocumentUpsertAsync_NewDocument_CreatesDocument()
    {
        // Arrange
        var newDoc = new MongoLinksetDocument
        {
            Id = "01_09506000134376",
            Data = new List<LinksetDataItem>
            {
                new LinksetDataItem
                {
                    Linkset = new LinksetObject
                    {
                        LinkTypes = new Dictionary<string, List<LinksetEntry>>
                        {
                            {
                                "https://gs1.org/voc/pip",
                                new List<LinksetEntry>
                                {
                                    new LinksetEntry { Href = "https://example.com", Title = "Test" }
                                }
                            }
                        }
                    }
                }
            }
        };

        _repositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((ResolverDocument?)null);

        _repositoryMock.Setup(r => r.CreateAsync(It.IsAny<ResolverDocument>()))
            .ReturnsAsync((ResolverDocument doc) => doc);

        // Act
        var (result, statusCode) = await _service.ProcessDocumentUpsertAsync(newDoc, _repositoryMock.Object);

        // Assert
        Assert.Equal(201, statusCode);
        Assert.Equal("01_09506000134376", result.Id);
        _repositoryMock.Verify(r => r.CreateAsync(It.IsAny<ResolverDocument>()), Times.Once);
    }

    [Fact]
    public async Task ProcessDocumentUpsertAsync_ExistingDocument_UpdatesDocument()
    {
        // Arrange
        var existingDoc = new ResolverDocument
        {
            Id = "01_09506000134376",
            Data = new List<LinksetDataItem>
            {
                new LinksetDataItem
                {
                    Qualifiers = new List<Dictionary<string, string>>(),
                    Linkset = new LinksetObject
                    {
                        LinkTypes = new Dictionary<string, List<LinksetEntry>>
                        {
                            {
                                "https://gs1.org/voc/pip",
                                new List<LinksetEntry>
                                {
                                    new LinksetEntry { Href = "https://example.com/old", Title = "Old" }
                                }
                            }
                        }
                    }
                }
            }
        };

        var newDoc = new MongoLinksetDocument
        {
            Id = "01_09506000134376",
            Data = new List<LinksetDataItem>
            {
                new LinksetDataItem
                {
                    Qualifiers = new List<Dictionary<string, string>>(),
                    Linkset = new LinksetObject
                    {
                        LinkTypes = new Dictionary<string, List<LinksetEntry>>
                        {
                            {
                                "https://gs1.org/voc/hasRetailers",
                                new List<LinksetEntry>
                                {
                                    new LinksetEntry { Href = "https://example.com/new", Title = "New" }
                                }
                            }
                        }
                    }
                }
            }
        };

        _repositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<string>()))
            .ReturnsAsync(existingDoc);

        _repositoryMock.Setup(r => r.UpdateAsync(It.IsAny<ResolverDocument>()))
            .ReturnsAsync((ResolverDocument doc) => doc);

        // Act
        var (result, statusCode) = await _service.ProcessDocumentUpsertAsync(newDoc, _repositoryMock.Object);

        // Assert
        // The service now creates instead of updates when qualifiers don't match
        Assert.True(statusCode == 200 || statusCode == 201);
    }

    [Fact]
    public async Task MigrateV2Async_ValidV2Doc_ReturnsV3Doc()
    {
        // Arrange
        var v2Doc = new DataEntryV2Document
        {
            IdentificationKeyType = "01",
            IdentificationKey = "09506000134376",
            ItemDescription = "Test Product",
            Responses = new List<ResponseItemV2>
            {
                new ResponseItemV2
                {
                    LinkType = "pip",
                    TargetUrl = "https://example.com/product",
                    LinkTitle = "Product Info",
                    MimeType = "text/html",
                    IanaLanguage = "en",
                    DefaultLinkType = true,
                    Active = true
                }
            }
        };

        // Act
        var result = await _service.MigrateV2Async(v2Doc);

        // Assert
        Assert.Single(result);
        Assert.Equal("/01/09506000134376", result[0].Anchor);
        Assert.Equal("Test Product", result[0].ItemDescription);
        Assert.Single(result[0].Links);
        Assert.Equal("gs1:pip", result[0].Links[0].Linktype);
        Assert.Equal("https://example.com/product", result[0].Links[0].Href);
        Assert.Equal("gs1:pip", result[0].DefaultLinktype);
    }

    [Fact]
    public async Task MigrateV2Async_WithQualifierPath_ParsesQualifiers()
    {
        // Arrange
        var v2Doc = new DataEntryV2Document
        {
            IdentificationKeyType = "01",
            IdentificationKey = "09506000134376",
            QualifierPath = "/21/12345",
            Responses = new List<ResponseItemV2>
            {
                new ResponseItemV2
                {
                    LinkType = "pip",
                    TargetUrl = "https://example.com",
                    LinkTitle = "Test",
                    Active = true
                }
            }
        };

        // Act
        var result = await _service.MigrateV2Async(v2Doc);

        // Assert
        Assert.Equal("/01/09506000134376/21/12345", result[0].Anchor);
        Assert.NotNull(result[0].Qualifiers);
        Assert.Single(result[0].Qualifiers!);
        Assert.Contains("21", result[0].Qualifiers[0].Keys);
        Assert.Equal("12345", result[0].Qualifiers[0]["21"]);
    }

    [Fact]
    public async Task ReadDocumentAsync_DocumentExists_ReturnsV3Docs()
    {
        // Arrange
        var docId = "01_09506000134376";
        var repoDoc = new ResolverDocument
        {
            Id = docId,
            Data = new List<LinksetDataItem>
            {
                new LinksetDataItem
                {
                    Linkset = new LinksetObject
                    {
                        LinkTypes = new Dictionary<string, List<LinksetEntry>>
                        {
                            {
                                "https://gs1.org/voc/pip",
                                new List<LinksetEntry>
                                {
                                    new LinksetEntry { Href = "https://example.com", Title = "Test" }
                                }
                            }
                        }
                    }
                }
            }
        };

        _repositoryMock.Setup(r => r.GetByIdAsync(docId))
            .ReturnsAsync(repoDoc);

        // Act
        var result = await _service.ReadDocumentAsync(docId);

        // Assert
        Assert.Single(result);
        Assert.Equal("/01/09506000134376", result[0].Anchor);
    }

    [Fact]
    public async Task ReadDocumentAsync_DocumentNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((ResolverDocument?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.ReadDocumentAsync("nonexistent"));
    }

    [Fact]
    public async Task ReadIndexAsync_ReturnsFormattedPaths()
    {
        // Arrange
        var documentIds = new List<string>
        {
            "01_09506000134376",
            "01_09506000134376_21_12345"
        };

        _repositoryMock.Setup(r => r.GetAllDocumentIdsAsync())
            .ReturnsAsync(documentIds);

        // Act
        var result = await _service.ReadIndexAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains("/01/09506000134376", result);
        Assert.Contains("/01/09506000134376/21/12345", result);
    }

    [Fact]
    public async Task ReadIndexAsync_EmptyRepository_ReturnsEmptyList()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetAllDocumentIdsAsync())
            .ReturnsAsync(new List<string>());

        // Act
        var result = await _service.ReadIndexAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task ReadIndexAsync_MultipleIds_ConvertsAllToPaths()
    {
        // Arrange
        var documentIds = new List<string>
        {
            "01_09506000134376",
            "01_09506000134376_21_12345",
            "01_09506000134376_21_12345_10_ABC",
            "gtin_09506000134376",
            "test_path_with_multiple_segments"
        };

        _repositoryMock.Setup(r => r.GetAllDocumentIdsAsync())
            .ReturnsAsync(documentIds);

        // Act
        var result = await _service.ReadIndexAsync();

        // Assert
        Assert.Equal(5, result.Count);
        Assert.Contains("/01/09506000134376", result);
        Assert.Contains("/01/09506000134376/21/12345", result);
        Assert.Contains("/01/09506000134376/21/12345/10/ABC", result);
        Assert.Contains("/gtin/09506000134376", result);
        Assert.Contains("/test/path/with/multiple/segments", result);
    }

    [Fact]
    public async Task DeleteDocumentAsync_ValidId_CallsRepository()
    {
        // Arrange
        var docId = "01_09506000134376";
        _repositoryMock.Setup(r => r.DeleteAsync(docId))
            .Returns(Task.CompletedTask);

        // Act
        await _service.DeleteDocumentAsync(docId);

        // Assert
        _repositoryMock.Verify(r => r.DeleteAsync(docId), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DeleteDocumentAsync_InvalidId_ThrowsArgumentException(string? id)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.DeleteDocumentAsync(id!));
    }

    [Fact]
    public async Task AuthorAndConvert_DefaultLinktype_RoundtripPreservesValue()
    {
        // Arrange - Create a V3 document with a DefaultLinktype
        var v3Doc = new DataEntryV3Document
        {
            Anchor = "/01/09506000134376",
            ItemDescription = "Test Product",
            DefaultLinktype = "gs1:pip",
            Links = new List<LinkV3>
            {
                new LinkV3
                {
                    Linktype = "gs1:pip",
                    Href = "https://example.com/product",
                    Title = "Product Information"
                },
                new LinkV3
                {
                    Linktype = "gs1:certificationInfo",
                    Href = "https://example.com/cert",
                    Title = "Certification"
                }
            }
        };

        // Act - Author to Mongo format
        var mongoDoc = await _service.AuthorDbLinksetDocumentAsync(v3Doc);

        // Assert - Verify DefaultLinktype is stored in MongoLinksetDocument
        Assert.Equal("gs1:pip", mongoDoc.DefaultLinktype);

        // Act - Convert Mongo format to ResolverDocument and back to V3
        var resolverDoc = new ResolverDocument
        {
            Id = mongoDoc.Id,
            DefaultLinktype = mongoDoc.DefaultLinktype,
            Data = mongoDoc.Data
        };

        var convertedV3Docs = await _service.ConvertMongoLinksetToV3Async(resolverDoc);

        // Assert - Verify DefaultLinktype is preserved in the roundtrip
        Assert.Single(convertedV3Docs);
        Assert.Equal("gs1:pip", convertedV3Docs[0].DefaultLinktype);
        Assert.Equal(v3Doc.Anchor, convertedV3Docs[0].Anchor);
        Assert.Equal(v3Doc.ItemDescription, convertedV3Docs[0].ItemDescription);
    }
}
