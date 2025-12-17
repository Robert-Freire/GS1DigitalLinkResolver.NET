using GS1Resolver.Shared.Models;
using GS1Resolver.Shared.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GS1Resolver.Shared.Tests.Services;

public class LinksetFormatterServiceTests
{
    private readonly LinksetFormatterService _service;
    private readonly Mock<ILogger<LinksetFormatterService>> _loggerMock;

    public LinksetFormatterServiceTests()
    {
        _loggerMock = new Mock<ILogger<LinksetFormatterService>>();
        _service = new LinksetFormatterService(_loggerMock.Object);
    }

    [Fact]
    public void GenerateLinkHeader_CreatesCorrectFormat()
    {
        // Arrange
        var linksetItems = new List<LinksetDataItem>();
        var identifier = "/01/09521234543213";
        var fqdn = "example.com";

        // Act
        var result = _service.GenerateLinkHeader(linksetItems, identifier, fqdn);

        // Assert
        Assert.Contains($"https://{fqdn}{identifier}?linkType=linkset", result);
        Assert.Contains("rel=\"application/linkset\"", result);
        Assert.Contains("type=\"application/linkset+json\"", result);
        Assert.Contains($"title=\"Linkset for {identifier}\"", result);
    }

    [Fact]
    public void FormatLinksetForExternalUse_IncludesJsonLdContext()
    {
        // Arrange
        var document = new ResolverDocument
        {
            Id = "01_09521234543213",
            Data = new List<LinksetDataItem>()
        };
        var matchedItems = new List<LinksetDataItem>();
        var identifier = "/01/09521234543213";
        var fqdn = "example.com";

        // Act
        var result = _service.FormatLinksetForExternalUse(document, matchedItems, identifier, fqdn);

        // Assert
        Assert.NotNull(result);
        var dict = result as Dictionary<string, object>;
        Assert.NotNull(dict);
        Assert.True(dict.ContainsKey("@context"));
        Assert.True(dict.ContainsKey("@id"));
        Assert.True(dict.ContainsKey("@type"));
    }

    [Fact]
    public void FormatLinksetForExternalUse_AddsGtinForAiCode01()
    {
        // Arrange
        var document = new ResolverDocument
        {
            Id = "01_09521234543213",
            Data = new List<LinksetDataItem>()
        };
        var matchedItems = new List<LinksetDataItem>();
        var identifier = "/01/09521234543213";
        var fqdn = "example.com";

        // Act
        var result = _service.FormatLinksetForExternalUse(document, matchedItems, identifier, fqdn);

        // Assert
        var dict = result as Dictionary<string, object>;
        Assert.NotNull(dict);
        Assert.True(dict.ContainsKey("gtin"));
        Assert.Equal("09521234543213", dict["gtin"]);
    }

    [Fact]
    public void FormatLinksetForExternalUse_SetsCorrectId()
    {
        // Arrange
        var document = new ResolverDocument
        {
            Id = "01_09521234543213",
            Data = new List<LinksetDataItem>()
        };
        var matchedItems = new List<LinksetDataItem>();
        var identifier = "/01/09521234543213";
        var fqdn = "example.com";

        // Act
        var result = _service.FormatLinksetForExternalUse(document, matchedItems, identifier, fqdn);

        // Assert
        var dict = result as Dictionary<string, object>;
        Assert.NotNull(dict);
        Assert.Equal($"https://{fqdn}{identifier}", dict["@id"]);
    }

    [Fact]
    public void FormatLinksetForExternalUse_IncludesElementStrings()
    {
        // Arrange
        var document = new ResolverDocument
        {
            Id = "01_09521234543213",
            Data = new List<LinksetDataItem>()
        };
        var matchedItems = new List<LinksetDataItem>();
        var identifier = "/01/09521234543213";
        var fqdn = "example.com";

        // Act
        var result = _service.FormatLinksetForExternalUse(document, matchedItems, identifier, fqdn);

        // Assert
        var dict = result as Dictionary<string, object>;
        Assert.NotNull(dict);
        Assert.True(dict.ContainsKey("gs1:elementStrings"));
        var elementStrings = dict["gs1:elementStrings"] as List<string>;
        Assert.NotNull(elementStrings);
        Assert.Contains(identifier, elementStrings);
    }

    [Fact]
    public void FormatLinksetForExternalUse_NormalizesRelativeHrefToFullyQualified()
    {
        // Arrange
        var document = new ResolverDocument
        {
            Id = "01_09521234543213",
            Data = new List<LinksetDataItem>()
        };
        var matchedItems = new List<LinksetDataItem>
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
                                new LinksetEntry
                                {
                                    Href = "/product/info",
                                    Type = "text/html",
                                    Title = "Product Info"
                                }
                            }
                        }
                    }
                }
            }
        };
        var identifier = "/01/09521234543213";
        var fqdn = "example.com";

        // Act
        var result = _service.FormatLinksetForExternalUse(document, matchedItems, identifier, fqdn);

        // Assert
        var dict = result as Dictionary<string, object>;
        Assert.NotNull(dict);
        var linkset = dict["linkset"] as List<Dictionary<string, object>>;
        Assert.NotNull(linkset);
        Assert.Single(linkset);

        var firstLinksetItem = linkset[0];
        Assert.True(firstLinksetItem.ContainsKey("https://gs1.org/voc/pip"));

        var pipEntries = firstLinksetItem["https://gs1.org/voc/pip"] as List<Dictionary<string, object>>;
        Assert.NotNull(pipEntries);
        Assert.Single(pipEntries);

        var entry = pipEntries[0];
        Assert.Equal($"https://{fqdn}/product/info", entry["href"]);
    }

    [Fact]
    public void FormatLinksetForExternalUse_PreservesAbsoluteHref()
    {
        // Arrange
        var document = new ResolverDocument
        {
            Id = "01_09521234543213",
            Data = new List<LinksetDataItem>()
        };
        var matchedItems = new List<LinksetDataItem>
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
                                new LinksetEntry
                                {
                                    Href = "https://external.example.org/product",
                                    Type = "text/html",
                                    Title = "External Product"
                                }
                            }
                        }
                    }
                }
            }
        };
        var identifier = "/01/09521234543213";
        var fqdn = "example.com";

        // Act
        var result = _service.FormatLinksetForExternalUse(document, matchedItems, identifier, fqdn);

        // Assert
        var dict = result as Dictionary<string, object>;
        Assert.NotNull(dict);
        var linkset = dict["linkset"] as List<Dictionary<string, object>>;
        Assert.NotNull(linkset);
        Assert.Single(linkset);

        var firstLinksetItem = linkset[0];
        Assert.True(firstLinksetItem.ContainsKey("https://gs1.org/voc/pip"));

        var pipEntries = firstLinksetItem["https://gs1.org/voc/pip"] as List<Dictionary<string, object>>;
        Assert.NotNull(pipEntries);
        Assert.Single(pipEntries);

        var entry = pipEntries[0];
        Assert.Equal("https://external.example.org/product", entry["href"]);
    }

    [Fact]
    public void FormatLinksetForExternalUse_NormalizesRelativePathWithoutLeadingSlash()
    {
        // Arrange
        var document = new ResolverDocument
        {
            Id = "01_09521234543213",
            Data = new List<LinksetDataItem>()
        };
        var matchedItems = new List<LinksetDataItem>
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
                                new LinksetEntry
                                {
                                    Href = "product/info/details",
                                    Type = "text/html",
                                    Title = "Product Details"
                                }
                            }
                        }
                    }
                }
            }
        };
        var identifier = "/01/09521234543213";
        var fqdn = "example.com";

        // Act
        var result = _service.FormatLinksetForExternalUse(document, matchedItems, identifier, fqdn);

        // Assert
        var dict = result as Dictionary<string, object>;
        Assert.NotNull(dict);
        var linkset = dict["linkset"] as List<Dictionary<string, object>>;
        Assert.NotNull(linkset);
        Assert.Single(linkset);

        var firstLinksetItem = linkset[0];
        Assert.True(firstLinksetItem.ContainsKey("https://gs1.org/voc/pip"));

        var pipEntries = firstLinksetItem["https://gs1.org/voc/pip"] as List<Dictionary<string, object>>;
        Assert.NotNull(pipEntries);
        Assert.Single(pipEntries);

        var entry = pipEntries[0];
        Assert.Equal($"https://{fqdn}/product/info/details", entry["href"]);
    }
}
