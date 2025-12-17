using GS1Resolver.Shared.Models;
using GS1Resolver.Shared.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GS1Resolver.Shared.Tests.Services;

public class ContentNegotiationServiceTests
{
    private readonly ContentNegotiationService _service;
    private readonly Mock<ILogger<ContentNegotiationService>> _loggerMock;

    public ContentNegotiationServiceTests()
    {
        _loggerMock = new Mock<ILogger<ContentNegotiationService>>();
        _service = new ContentNegotiationService(_loggerMock.Object);
    }

    [Fact]
    public void CleanQValues_RemovesQualityValues()
    {
        // Arrange
        var input = new List<string> { "en;q=0.9", "fr;q=0.8", "de" };

        // Act
        var result = _service.CleanQValues(input);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("en", result[0]);
        Assert.Equal("fr", result[1]);
        Assert.Equal("de", result[2]);
    }

    [Fact]
    public void GetAppropriateLinksetEntries_MatchesAllThreeContexts()
    {
        // Arrange
        var entries = new List<LinksetEntry>
        {
            new LinksetEntry
            {
                Href = "http://example.com/1",
                Hreflang = new List<string> { "en" },
                Context = new List<string> { "retail" },
                Type = "text/html"
            },
            new LinksetEntry
            {
                Href = "http://example.com/2",
                Hreflang = new List<string> { "fr" },
                Context = new List<string> { "wholesale" },
                Type = "text/html"
            }
        };

        var languages = new List<string> { "en" };
        var context = "retail";
        var mediaTypes = new List<string> { "text/html" };

        // Act
        var result = _service.GetAppropriateLinksetEntries(entries, languages, context, mediaTypes);

        // Assert
        Assert.Single(result);
        Assert.Equal("http://example.com/1", result[0].Href);
    }

    [Fact]
    public void GetAppropriateLinksetEntries_MatchesLanguageOnly()
    {
        // Arrange
        var entries = new List<LinksetEntry>
        {
            new LinksetEntry
            {
                Href = "http://example.com/1",
                Hreflang = new List<string> { "en" },
                Type = "text/html"
            },
            new LinksetEntry
            {
                Href = "http://example.com/2",
                Hreflang = new List<string> { "fr" },
                Type = "text/html"
            }
        };

        var languages = new List<string> { "en" };

        // Act
        var result = _service.GetAppropriateLinksetEntries(entries, languages, null, null);

        // Assert
        Assert.Single(result);
        Assert.Equal("http://example.com/1", result[0].Href);
    }

    [Fact]
    public void GetAppropriateLinksetEntries_FallsBackToUndefinedLanguage()
    {
        // Arrange
        var entries = new List<LinksetEntry>
        {
            new LinksetEntry
            {
                Href = "http://example.com/1",
                Hreflang = new List<string> { "und" },
                Type = "text/html"
            }
        };

        var languages = new List<string> { "zh" }; // No match for Chinese

        // Act
        var result = _service.GetAppropriateLinksetEntries(entries, languages, null, null);

        // Assert
        Assert.Single(result);
        Assert.Equal("http://example.com/1", result[0].Href);
    }

    [Fact]
    public void GetAppropriateLinksetEntries_FallsBackToFirstEntry()
    {
        // Arrange
        var entries = new List<LinksetEntry>
        {
            new LinksetEntry
            {
                Href = "http://example.com/first",
                Hreflang = new List<string> { "fr" },
                Type = "application/pdf"
            },
            new LinksetEntry
            {
                Href = "http://example.com/second",
                Hreflang = new List<string> { "de" },
                Type = "application/xml"
            }
        };

        var languages = new List<string> { "en" }; // No match
        var mediaTypes = new List<string> { "text/html" }; // No match

        // Act
        var result = _service.GetAppropriateLinksetEntries(entries, languages, null, mediaTypes);

        // Assert
        Assert.Single(result);
        Assert.Equal("http://example.com/first", result[0].Href);
    }

    [Fact]
    public void GetAppropriateLinksetEntries_HandlesWildcardMediaType()
    {
        // Arrange
        var entries = new List<LinksetEntry>
        {
            new LinksetEntry
            {
                Href = "http://example.com/1",
                Type = "text/html"
            }
        };

        var mediaTypes = new List<string> { "*/*" };

        // Act
        var result = _service.GetAppropriateLinksetEntries(entries, new List<string> { "und" }, null, mediaTypes);

        // Assert
        Assert.Single(result);
    }

    [Fact]
    public void GetAppropriateLinksetEntries_MatchesLanguageAndContext()
    {
        // Arrange
        var entries = new List<LinksetEntry>
        {
            new LinksetEntry
            {
                Href = "http://example.com/1",
                Hreflang = new List<string> { "en" },
                Context = new List<string> { "retail" }
            },
            new LinksetEntry
            {
                Href = "http://example.com/2",
                Hreflang = new List<string> { "en" },
                Context = new List<string> { "wholesale" }
            }
        };

        var languages = new List<string> { "en" };
        var context = "retail";

        // Act
        var result = _service.GetAppropriateLinksetEntries(entries, languages, context, null);

        // Assert
        Assert.Single(result);
        Assert.Equal("http://example.com/1", result[0].Href);
    }

    [Fact]
    public void GetAppropriateLinksetEntries_EmptyList_ReturnsEmpty()
    {
        // Arrange
        var entries = new List<LinksetEntry>();
        var languages = new List<string> { "en" };

        // Act
        var result = _service.GetAppropriateLinksetEntries(entries, languages, null, null);

        // Assert
        Assert.Empty(result);
    }
}
