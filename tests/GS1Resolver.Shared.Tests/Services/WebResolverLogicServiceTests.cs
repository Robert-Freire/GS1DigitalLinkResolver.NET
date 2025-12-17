using GS1Resolver.Shared.Configuration;
using GS1Resolver.Shared.Models;
using GS1Resolver.Shared.Repositories;
using GS1Resolver.Shared.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GS1Resolver.Shared.Tests.Services;

public class WebResolverLogicServiceTests
{
    private readonly Mock<IResolverRepository> _repositoryMock;
    private readonly Mock<IGS1ToolkitService> _gs1ToolkitMock;
    private readonly Mock<IContentNegotiationService> _contentNegotiationMock;
    private readonly Mock<ILinksetFormatterService> _linksetFormatterMock;
    private readonly Mock<ILogger<WebResolverLogicService>> _loggerMock;
    private readonly IOptions<FqdnSettings> _fqdnSettings;
    private readonly WebResolverLogicService _service;

    public WebResolverLogicServiceTests()
    {
        _repositoryMock = new Mock<IResolverRepository>();
        _gs1ToolkitMock = new Mock<IGS1ToolkitService>();
        _contentNegotiationMock = new Mock<IContentNegotiationService>();
        _linksetFormatterMock = new Mock<ILinksetFormatterService>();
        _loggerMock = new Mock<ILogger<WebResolverLogicService>>();
        _fqdnSettings = Options.Create(new FqdnSettings { DomainName = "example.com" });

        _service = new WebResolverLogicService(
            _repositoryMock.Object,
            _gs1ToolkitMock.Object,
            _contentNegotiationMock.Object,
            _linksetFormatterMock.Object,
            _loggerMock.Object,
            _fqdnSettings
        );
    }

    [Fact]
    public async Task ResolveAsync_ValidIdentifier_Returns307Redirect()
    {
        // Arrange
        var identifier = "/01/09521234543213";
        var document = new ResolverDocument
        {
            Id = "01_09521234543213",
            Data = new List<LinksetDataItem>
            {
                new LinksetDataItem
                {
                    Linkset = new LinksetObject
                    {
                        LinkTypes = new Dictionary<string, List<LinksetEntry>>
                        {
                            ["https://gs1.org/voc/pip"] = new List<LinksetEntry>
                            {
                                new LinksetEntry
                                {
                                    Href = "http://example.com/product",
                                    Type = "text/html",
                                    Hreflang = new List<string> { "en" }
                                }
                            }
                        }
                    }
                }
            }
        };

        var context = new ResolverRequestContext(
            Linktype: "gs1:pip",
            Context: null,
            AcceptLanguageList: new List<string> { "en" },
            MediaTypesList: new List<string> { "text/html" },
            LinksetRequested: false,
            Compress: false
        );

        _gs1ToolkitMock.Setup(x => x.TestDigitalLinkSyntaxAsync(identifier))
            .ReturnsAsync(true);

        _repositoryMock.Setup(x => x.GetByIdAsync("01_09521234543213"))
            .ReturnsAsync(document);

        _contentNegotiationMock.Setup(x => x.GetAppropriateLinksetEntries(
                It.IsAny<List<LinksetEntry>>(),
                It.IsAny<List<string>>(),
                It.IsAny<string>(),
                It.IsAny<List<string>>(),
                It.IsAny<bool>()))
            .Returns(new List<LinksetEntry> { document.Data[0].Linkset.LinkTypes["https://gs1.org/voc/pip"][0] });

        _linksetFormatterMock.Setup(x => x.GenerateLinkHeader(
                It.IsAny<List<LinksetDataItem>>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns("<link-header>");

        // Act
        var result = await _service.ResolveAsync(identifier, null, context);

        // Assert
        Assert.Equal(307, result.StatusCode);
        Assert.Equal("http://example.com/product", result.LocationHeader);
        Assert.NotNull(result.LinkHeader);
    }

    [Fact]
    public async Task ResolveAsync_DocumentNotFound_Returns404()
    {
        // Arrange
        var identifier = "/01/09521234543213";
        var context = new ResolverRequestContext(
            Linktype: null,
            Context: null,
            AcceptLanguageList: new List<string> { "en" },
            MediaTypesList: new List<string> { "text/html" },
            LinksetRequested: false,
            Compress: false
        );

        _gs1ToolkitMock.Setup(x => x.TestDigitalLinkSyntaxAsync(identifier))
            .ReturnsAsync(true);

        _repositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((ResolverDocument?)null);

        // Act
        var result = await _service.ResolveAsync(identifier, null, context);

        // Assert
        Assert.Equal(404, result.StatusCode);
        Assert.Contains("No resolver document found", result.ErrorMessage);
    }

    [Fact]
    public async Task ResolveAsync_LinksetRequested_Returns200WithLinkset()
    {
        // Arrange
        var identifier = "/01/09521234543213";
        var document = new ResolverDocument
        {
            Id = "01_09521234543213",
            Data = new List<LinksetDataItem>
            {
                new LinksetDataItem
                {
                    Linkset = new LinksetObject
                    {
                        LinkTypes = new Dictionary<string, List<LinksetEntry>>
                        {
                            ["https://gs1.org/voc/pip"] = new List<LinksetEntry>
                            {
                                new LinksetEntry
                                {
                                    Href = "http://example.com/product",
                                    Type = "text/html",
                                    Hreflang = new List<string> { "en" }
                                }
                            }
                        }
                    }
                }
            }
        };

        var context = new ResolverRequestContext(
            Linktype: "all",
            Context: null,
            AcceptLanguageList: new List<string> { "en" },
            MediaTypesList: new List<string> { "application/json" },
            LinksetRequested: true,
            Compress: false
        );

        _gs1ToolkitMock.Setup(x => x.TestDigitalLinkSyntaxAsync(identifier))
            .ReturnsAsync(true);

        _repositoryMock.Setup(x => x.GetByIdAsync("01_09521234543213"))
            .ReturnsAsync(document);

        _linksetFormatterMock.Setup(x => x.FormatLinksetForExternalUse(
                It.IsAny<ResolverDocument>(),
                It.IsAny<List<LinksetDataItem>>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns(new { linkset = "formatted" });

        _linksetFormatterMock.Setup(x => x.GenerateLinkHeader(
                It.IsAny<List<LinksetDataItem>>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns("<link-header>");

        // Act
        var result = await _service.ResolveAsync(identifier, null, context);

        // Assert
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task ResolveAsync_MultipleMatches_Returns300()
    {
        // Arrange
        var identifier = "/01/09521234543213";
        var document = new ResolverDocument
        {
            Id = "01_09521234543213",
            Data = new List<LinksetDataItem>
            {
                new LinksetDataItem
                {
                    Linkset = new LinksetObject
                    {
                        LinkTypes = new Dictionary<string, List<LinksetEntry>>
                        {
                            ["https://gs1.org/voc/pip"] = new List<LinksetEntry>
                            {
                                new LinksetEntry { Href = "http://example.com/1" },
                                new LinksetEntry { Href = "http://example.com/2" }
                            }
                        }
                    }
                }
            }
        };

        var context = new ResolverRequestContext(
            Linktype: "gs1:pip",
            Context: null,
            AcceptLanguageList: new List<string> { "en" },
            MediaTypesList: new List<string> { "text/html" },
            LinksetRequested: false,
            Compress: false
        );

        _gs1ToolkitMock.Setup(x => x.TestDigitalLinkSyntaxAsync(identifier))
            .ReturnsAsync(true);

        _repositoryMock.Setup(x => x.GetByIdAsync("01_09521234543213"))
            .ReturnsAsync(document);

        _contentNegotiationMock.Setup(x => x.GetAppropriateLinksetEntries(
                It.IsAny<List<LinksetEntry>>(),
                It.IsAny<List<string>>(),
                It.IsAny<string>(),
                It.IsAny<List<string>>(),
                It.IsAny<bool>()))
            .Returns(document.Data[0].Linkset.LinkTypes["https://gs1.org/voc/pip"]);

        _linksetFormatterMock.Setup(x => x.GenerateLinkHeader(
                It.IsAny<List<LinksetDataItem>>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns("<link-header>");

        // Act
        var result = await _service.ResolveAsync(identifier, null, context);

        // Assert
        Assert.Equal(300, result.StatusCode);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task ResolveAsync_InvalidSyntax_ThrowsException()
    {
        // Arrange
        var identifier = "/invalid";
        var context = new ResolverRequestContext(
            Linktype: null,
            Context: null,
            AcceptLanguageList: new List<string> { "en" },
            MediaTypesList: new List<string> { "text/html" },
            LinksetRequested: false,
            Compress: false
        );

        _gs1ToolkitMock.Setup(x => x.TestDigitalLinkSyntaxAsync(identifier))
            .ReturnsAsync(false);

        // Act
        var result = await _service.ResolveAsync(identifier, null, context);

        // Assert
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task ResolveAsync_MultipleItemsWithDifferentQualifiers_FiltersCorrectly()
    {
        // Arrange
        var identifier = "/01/09521234543213";
        var qualifierPath = "/21/12345";
        var document = new ResolverDocument
        {
            Id = "01_09521234543213",
            Data = new List<LinksetDataItem>
            {
                // Item 1: has qualifier "21"
                new LinksetDataItem
                {
                    Qualifiers = new List<Dictionary<string, string>>
                    {
                        new Dictionary<string, string> { { "21", "{serial}" } }
                    },
                    Linkset = new LinksetObject
                    {
                        LinkTypes = new Dictionary<string, List<LinksetEntry>>
                        {
                            ["https://gs1.org/voc/pip"] = new List<LinksetEntry>
                            {
                                new LinksetEntry
                                {
                                    Href = "http://example.com/serial-product",
                                    Type = "text/html"
                                }
                            }
                        }
                    }
                },
                // Item 2: has qualifier "10" (should NOT match)
                new LinksetDataItem
                {
                    Qualifiers = new List<Dictionary<string, string>>
                    {
                        new Dictionary<string, string> { { "10", "LOT123" } }
                    },
                    Linkset = new LinksetObject
                    {
                        LinkTypes = new Dictionary<string, List<LinksetEntry>>
                        {
                            ["https://gs1.org/voc/pip"] = new List<LinksetEntry>
                            {
                                new LinksetEntry
                                {
                                    Href = "http://example.com/lot-product",
                                    Type = "text/html"
                                }
                            }
                        }
                    }
                },
                // Item 3: has no qualifiers (should NOT match when qualifiers provided)
                new LinksetDataItem
                {
                    Qualifiers = new List<Dictionary<string, string>>(),
                    Linkset = new LinksetObject
                    {
                        LinkTypes = new Dictionary<string, List<LinksetEntry>>
                        {
                            ["https://gs1.org/voc/pip"] = new List<LinksetEntry>
                            {
                                new LinksetEntry
                                {
                                    Href = "http://example.com/generic-product",
                                    Type = "text/html"
                                }
                            }
                        }
                    }
                }
            }
        };

        var context = new ResolverRequestContext(
            Linktype: "gs1:pip",
            Context: null,
            AcceptLanguageList: new List<string> { "en" },
            MediaTypesList: new List<string> { "text/html" },
            LinksetRequested: false,
            Compress: false
        );

        _gs1ToolkitMock.Setup(x => x.TestDigitalLinkSyntaxAsync(identifier + qualifierPath))
            .ReturnsAsync(true);

        _repositoryMock.Setup(x => x.GetByIdAsync("01_09521234543213"))
            .ReturnsAsync(document);

        _contentNegotiationMock.Setup(x => x.GetAppropriateLinksetEntries(
                It.IsAny<List<LinksetEntry>>(),
                It.IsAny<List<string>>(),
                It.IsAny<string>(),
                It.IsAny<List<string>>(),
                It.IsAny<bool>()))
            .Returns<List<LinksetEntry>, List<string>, string, List<string>, bool>((entries, _, _, _, _) =>
                entries.Take(1).ToList());

        _linksetFormatterMock.Setup(x => x.GenerateLinkHeader(
                It.IsAny<List<LinksetDataItem>>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns("<link-header>");

        // Act
        var result = await _service.ResolveAsync(identifier, qualifierPath, context);

        // Assert
        Assert.Equal(307, result.StatusCode);
        Assert.Equal("http://example.com/serial-product", result.LocationHeader);

        // Verify that only the item with matching qualifier "21" was considered
        _contentNegotiationMock.Verify(x => x.GetAppropriateLinksetEntries(
            It.Is<List<LinksetEntry>>(entries =>
                entries.Count == 1 &&
                entries[0].Href == "http://example.com/serial-product"),
            It.IsAny<List<string>>(),
            It.IsAny<string>(),
            It.IsAny<List<string>>(),
            It.IsAny<bool>()), Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_MultipleItemsBothMatchQualifiers_IncludesBothItems()
    {
        // Arrange
        var identifier = "/01/09521234543213";
        var qualifierPath = "/21/ABC";
        var document = new ResolverDocument
        {
            Id = "01_09521234543213",
            Data = new List<LinksetDataItem>
            {
                // Item 1: has qualifier "21" with template
                new LinksetDataItem
                {
                    Qualifiers = new List<Dictionary<string, string>>
                    {
                        new Dictionary<string, string> { { "21", "{serial}" } }
                    },
                    Linkset = new LinksetObject
                    {
                        LinkTypes = new Dictionary<string, List<LinksetEntry>>
                        {
                            ["https://gs1.org/voc/pip"] = new List<LinksetEntry>
                            {
                                new LinksetEntry
                                {
                                    Href = "http://example.com/product1?serial={serial}",
                                    Type = "text/html"
                                }
                            }
                        }
                    }
                },
                // Item 2: also has qualifier "21" with template (should also match)
                new LinksetDataItem
                {
                    Qualifiers = new List<Dictionary<string, string>>
                    {
                        new Dictionary<string, string> { { "21", "{serial}" } }
                    },
                    Linkset = new LinksetObject
                    {
                        LinkTypes = new Dictionary<string, List<LinksetEntry>>
                        {
                            ["https://gs1.org/voc/certificationInfo"] = new List<LinksetEntry>
                            {
                                new LinksetEntry
                                {
                                    Href = "http://example.com/cert?serial={serial}",
                                    Type = "application/json"
                                }
                            }
                        }
                    }
                }
            }
        };

        var context = new ResolverRequestContext(
            Linktype: "all",
            Context: null,
            AcceptLanguageList: new List<string> { "en" },
            MediaTypesList: new List<string> { "text/html" },
            LinksetRequested: true,
            Compress: false
        );

        _gs1ToolkitMock.Setup(x => x.TestDigitalLinkSyntaxAsync(identifier + qualifierPath))
            .ReturnsAsync(true);

        _repositoryMock.Setup(x => x.GetByIdAsync("01_09521234543213"))
            .ReturnsAsync(document);

        _linksetFormatterMock.Setup(x => x.FormatLinksetForExternalUse(
                It.IsAny<ResolverDocument>(),
                It.IsAny<List<LinksetDataItem>>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns<ResolverDocument, List<LinksetDataItem>, string, string>((_, items, _, _) =>
                new { itemCount = items.Count });

        _linksetFormatterMock.Setup(x => x.GenerateLinkHeader(
                It.IsAny<List<LinksetDataItem>>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns("<link-header>");

        // Act
        var result = await _service.ResolveAsync(identifier, qualifierPath, context);

        // Assert
        Assert.Equal(200, result.StatusCode);

        // Verify both items were included (both matched the qualifier)
        _linksetFormatterMock.Verify(x => x.FormatLinksetForExternalUse(
            It.IsAny<ResolverDocument>(),
            It.Is<List<LinksetDataItem>>(items => items.Count == 2),
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_NoItemsMatchQualifiers_Returns404()
    {
        // Arrange
        var identifier = "/01/09521234543213";
        var qualifierPath = "/21/12345";
        var document = new ResolverDocument
        {
            Id = "01_09521234543213",
            Data = new List<LinksetDataItem>
            {
                // Item has qualifier "10" but request has "21" (no match)
                new LinksetDataItem
                {
                    Qualifiers = new List<Dictionary<string, string>>
                    {
                        new Dictionary<string, string> { { "10", "LOT123" } }
                    },
                    Linkset = new LinksetObject
                    {
                        LinkTypes = new Dictionary<string, List<LinksetEntry>>
                        {
                            ["https://gs1.org/voc/pip"] = new List<LinksetEntry>
                            {
                                new LinksetEntry { Href = "http://example.com/lot-product" }
                            }
                        }
                    }
                }
            }
        };

        var context = new ResolverRequestContext(
            Linktype: "gs1:pip",
            Context: null,
            AcceptLanguageList: new List<string> { "en" },
            MediaTypesList: new List<string> { "text/html" },
            LinksetRequested: false,
            Compress: false
        );

        _gs1ToolkitMock.Setup(x => x.TestDigitalLinkSyntaxAsync(identifier + qualifierPath))
            .ReturnsAsync(true);

        _repositoryMock.Setup(x => x.GetByIdAsync("01_09521234543213"))
            .ReturnsAsync(document);

        // Act
        var result = await _service.ResolveAsync(identifier, qualifierPath, context);

        // Assert
        Assert.Equal(404, result.StatusCode);
        Assert.Contains("No matching qualifiers found", result.ErrorMessage);
    }

    [Fact]
    public async Task ResolveAsync_QualifierTemplateVariables_ReplacedCorrectly()
    {
        // Arrange
        var identifier = "/01/09521234543213";
        var qualifierPath = "/21/SN12345";
        var document = new ResolverDocument
        {
            Id = "01_09521234543213",
            Data = new List<LinksetDataItem>
            {
                new LinksetDataItem
                {
                    Qualifiers = new List<Dictionary<string, string>>
                    {
                        new Dictionary<string, string> { { "21", "{serial}" } }
                    },
                    Linkset = new LinksetObject
                    {
                        LinkTypes = new Dictionary<string, List<LinksetEntry>>
                        {
                            ["https://gs1.org/voc/pip"] = new List<LinksetEntry>
                            {
                                new LinksetEntry
                                {
                                    Href = "http://example.com/product?serial={serial}",
                                    Type = "text/html"
                                }
                            }
                        }
                    }
                }
            }
        };

        var context = new ResolverRequestContext(
            Linktype: "gs1:pip",
            Context: null,
            AcceptLanguageList: new List<string> { "en" },
            MediaTypesList: new List<string> { "text/html" },
            LinksetRequested: false,
            Compress: false
        );

        _gs1ToolkitMock.Setup(x => x.TestDigitalLinkSyntaxAsync(identifier + qualifierPath))
            .ReturnsAsync(true);

        _repositoryMock.Setup(x => x.GetByIdAsync("01_09521234543213"))
            .ReturnsAsync(document);

        _contentNegotiationMock.Setup(x => x.GetAppropriateLinksetEntries(
                It.IsAny<List<LinksetEntry>>(),
                It.IsAny<List<string>>(),
                It.IsAny<string>(),
                It.IsAny<List<string>>(),
                It.IsAny<bool>()))
            .Returns<List<LinksetEntry>, List<string>, string, List<string>, bool>((entries, _, _, _, _) =>
                entries.Take(1).ToList());

        _linksetFormatterMock.Setup(x => x.GenerateLinkHeader(
                It.IsAny<List<LinksetDataItem>>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns("<link-header>");

        // Act
        var result = await _service.ResolveAsync(identifier, qualifierPath, context);

        // Assert
        Assert.Equal(307, result.StatusCode);
        Assert.Equal("http://example.com/product?serial=SN12345", result.LocationHeader);
    }
}
