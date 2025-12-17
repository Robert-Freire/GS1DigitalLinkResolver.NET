using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using GS1Resolver.Shared.Models;
using GS1Resolver.Shared.Tests.Fixtures;
using GS1Resolver.Shared.Tests.TestData;

namespace GS1Resolver.Shared.Tests.Integration;

[CollectionDefinition("IntegrationSequential", DisableParallelization = true)]
public class IntegrationSequentialCollection
{
}

[Trait("Category", "Integration")]
[Collection("IntegrationSequential")]
public class GS1ResolverEndToEndTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;
    private readonly HttpClient _dataEntryClient;
    private readonly HttpClient _webResolverClient;
    private readonly List<TestDataEntry> _testDataEntries;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private const bool DELETE_ENTRIES_ON_COMPLETION = false;

    public GS1ResolverEndToEndTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
        _dataEntryClient = fixture.DataEntryClient;
        _webResolverClient = fixture.WebResolverClient;
        _testDataEntries = fixture.TestDataEntries;

        // Fail immediately if toolkit unavailable
        if (_fixture.SkipReason != null)
        {
            throw new InvalidOperationException($"E2E tests cannot run: {_fixture.SkipReason}");
        }
    }

    #region CRUD Cycle Tests

    [Fact]
    public async Task Test01_InitialDelete_ShouldReturn200Or404()
    {
        // Iterate through all test data entries and delete them
        foreach (var testEntry in _testDataEntries)
        {
            foreach (var document in testEntry.Documents)
            {
                var anchor = TestDataLoader.ExtractAnchor(document);
                var response = await DeleteEntryAsync(anchor);

                // Should return 200 (success) or 404 (not found) - both are acceptable
                response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.NoContent);
            }
        }
    }

    [Fact]
    public async Task Test02_CreateEntries_ShouldReturn201()
    {
        // Iterate through all test data entries and create them
        foreach (var testEntry in _testDataEntries)
        {
            foreach (var document in testEntry.Documents)
            {
                var response = await CreateEntryAsync(document);

                // Should return 200 or 201
                response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);

                var content = await response.Content.ReadAsStringAsync();
                content.Should().NotBeNullOrEmpty();
            }
        }
    }

    [Fact]
    public async Task Test03_ReadEntries_ShouldReturn200WithMatchingData()
    {
        // First create the entries
        await Test02_CreateEntries_ShouldReturn201();

        // Now read them back and verify
        foreach (var testEntry in _testDataEntries)
        {
            foreach (var document in testEntry.Documents)
            {
                var anchor = TestDataLoader.ExtractAnchor(document);
                var response = await GetEntryAsync(anchor);

                response.StatusCode.Should().Be(HttpStatusCode.OK);

                var content = await response.Content.ReadAsStringAsync();
                content.Should().NotBeNullOrEmpty();

                // Deserialize the data array into strongly typed DataEntryV3Document objects
                var returnedDocuments = JsonSerializer.Deserialize<List<DataEntryV3Document>>(content, JsonOptions);
                returnedDocuments.Should().NotBeNull();
                returnedDocuments.Should().HaveCountGreaterThan(0, "The response should contain at least one document");

                // For anchors with multiple qualifiers (like 01/09506000134376), we should get multiple V3 documents back
                // Each data item becomes a separate V3 document

                // Locate the corresponding document in the returned data by matching ItemDescription
                // Since each data item now has its own ItemDescription, that's the unique identifier
                var returnedDocument = returnedDocuments!.FirstOrDefault(d =>
                    d.ItemDescription == document.ItemDescription);

                // If not found, try without case sensitivity or trim
                if (returnedDocument == null)
                {
                    returnedDocument = returnedDocuments!.FirstOrDefault(d =>
                        string.Equals(d.ItemDescription?.Trim(), document.ItemDescription?.Trim(), StringComparison.OrdinalIgnoreCase));
                }

                returnedDocument.Should().NotBeNull($"Document with ItemDescription '{document.ItemDescription}' should be in the returned data. " +
                    $"Returned documents have ItemDescriptions: {string.Join(", ", returnedDocuments!.Select(d => $"'{d.ItemDescription}'"))}");

                // Assert that key properties match the source document
                returnedDocument!.Anchor.Should().Be(document.Anchor, "Anchor should match");
                returnedDocument.ItemDescription.Should().Be(document.ItemDescription, "ItemDescription should match");
                returnedDocument.DefaultLinktype.Should().Be(document.DefaultLinktype, "DefaultLinktype should match");

                // Compare Qualifiers
                if (document.Qualifiers == null)
                {
                    returnedDocument.Qualifiers.Should().BeNullOrEmpty("Qualifiers should be null or empty when source is null");
                }
                else
                {
                    returnedDocument.Qualifiers.Should().NotBeNull("Qualifiers should not be null when source has qualifiers");
                    returnedDocument.Qualifiers.Should().HaveCount(document.Qualifiers.Count, "Qualifiers count should match");

                    for (int i = 0; i < document.Qualifiers.Count; i++)
                    {
                        var expectedQualifier = document.Qualifiers[i];
                        var actualQualifier = returnedDocument.Qualifiers![i];

                        actualQualifier.Should().BeEquivalentTo(expectedQualifier,
                            $"Qualifier at index {i} should match");
                    }
                }

                // Compare Links
                returnedDocument.Links.Should().NotBeNull("Links should not be null");
                returnedDocument.Links.Should().HaveCount(document.Links.Count, "Links count should match");

                for (int i = 0; i < document.Links.Count; i++)
                {
                    var expectedLink = document.Links[i];
                    var actualLink = returnedDocument.Links[i];

                    actualLink.Linktype.Should().Be(expectedLink.Linktype, $"Link {i} Linktype should match");
                    actualLink.Href.Should().Be(expectedLink.Href, $"Link {i} Href should match");
                    actualLink.Title.Should().Be(expectedLink.Title, $"Link {i} Title should match");
                    actualLink.Type.Should().Be(expectedLink.Type, $"Link {i} Type should match");

                    // Compare Hreflang
                    if (expectedLink.Hreflang == null)
                    {
                        actualLink.Hreflang.Should().BeNullOrEmpty($"Link {i} Hreflang should be null or empty when source is null");
                    }
                    else
                    {
                        actualLink.Hreflang.Should().NotBeNull($"Link {i} Hreflang should not be null when source has values");
                        actualLink.Hreflang.Should().BeEquivalentTo(expectedLink.Hreflang, $"Link {i} Hreflang should match");
                    }

                    // Compare Context
                    if (expectedLink.Context == null)
                    {
                        actualLink.Context.Should().BeNullOrEmpty($"Link {i} Context should be null or empty when source is null");
                    }
                    else
                    {
                        actualLink.Context.Should().NotBeNull($"Link {i} Context should not be null when source has values");
                        actualLink.Context.Should().BeEquivalentTo(expectedLink.Context, $"Link {i} Context should match");
                    }
                }
            }
        }
    }

    [Fact]
    public async Task Test04_DeleteEntries_ShouldReturn200Or404()
    {
        if (!DELETE_ENTRIES_ON_COMPLETION)
        {
            // Skip deletion
            return;
        }

        // Delete all entries for cleanup
        foreach (var testEntry in _testDataEntries)
        {
            foreach (var document in testEntry.Documents)
            {
                var anchor = TestDataLoader.ExtractAnchor(document);
                var response = await DeleteEntryAsync(anchor);

                response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.NoContent);
            }
        }
    }

    #endregion

    #region GS1 Digital Link Resolution Tests

    [Fact]
    public async Task Test05_ResolveGtin_ShouldReturn307Redirect()
    {
        // Ensure data is created
        await Test02_CreateEntries_ShouldReturn201();

        var response = await ResolveAsync("/01/09506000134376");

        response.StatusCode.Should().Be(HttpStatusCode.RedirectKeepVerb); // 307
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Be("https://dalgiardino.com/medicinal-compound/pil.html");
    }

    [Fact]
    public async Task Test06_ResolveSerializedGtin_ShouldReturn307WithSerial()
    {
        // Ensure data is created
        await Test02_CreateEntries_ShouldReturn201();

        var response = await ResolveAsync("/01/09506000134376/21/HELLOWORLD");

        response.StatusCode.Should().Be(HttpStatusCode.RedirectKeepVerb); // 307
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain("https://dalgiardino.com/medicinal-compound/pil.html");
        response.Headers.Location!.ToString().Should().Contain("serial=HELLOWORLD");
    }

    [Fact]
    public async Task Test07_ResolveLotNumberedGtin_ShouldReturn307WithLot()
    {
        // Ensure data is created
        await Test02_CreateEntries_ShouldReturn201();

        var response = await ResolveAsync("/01/09506000134376/10/LOT01");

        response.StatusCode.Should().Be(HttpStatusCode.RedirectKeepVerb); // 307
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain("https://dalgiardino.com/medicinal-compound/pil.html");
        response.Headers.Location!.ToString().Should().Contain("lot=LOT01");
    }

    #endregion

    #region Compression Tests

    [Fact]
    public async Task Test08_CompressDigitalLink_ShouldReturnCompressedPath()
    {
        // Ensure data is created
        await Test02_CreateEntries_ShouldReturn201();

        var response = await ResolveAsync("/01/09506000134376/10/LOT01?compress=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content, JsonOptions);

        result.TryGetProperty("compressedLink", out var compressedLink).Should().BeTrue();
        compressedLink.GetString().Should().Be("/ARFKk4XB0CDKWcnpq");
    }

    [Fact]
    public async Task Test09_ResolveCompressedLink_ShouldReturn307()
    {
        // Ensure data is created
        await Test02_CreateEntries_ShouldReturn201();

        // Use the compressed link from Test08
        var response = await ResolveAsync("/ARFKk4XB0CDKWcnpq");

        response.StatusCode.Should().Be(HttpStatusCode.RedirectKeepVerb); // 307
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain("https://dalgiardino.com/medicinal-compound/pil.html");
        response.Headers.Location!.ToString().Should().Contain("lot=LOT01");
    }

    #endregion

    #region Linkset Tests

    [Fact]
    public async Task Test10_GetLinksetAsJson_ShouldReturn200WithLinkset()
    {
        // Ensure data is created
        await Test02_CreateEntries_ShouldReturn201();

        // Test with application/json
        var headers1 = new Dictionary<string, string>
        {
            ["Accept"] = "application/json"
        };
        var response1 = await ResolveAsync("/01/09506000134376", headers1);

        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        var content1 = await response1.Content.ReadAsStringAsync();
        var result1 = JsonSerializer.Deserialize<JsonElement>(content1, JsonOptions);

        // Root object
        result1.ValueKind.Should().Be(JsonValueKind.Object);

        result1.TryGetProperty("@id", out var idElement).Should().BeTrue();
        idElement.GetString().Should().Contain("/01/09506000134376");

        result1.TryGetProperty("linkset", out var linksetElement).Should().BeTrue();
        linksetElement.ValueKind.Should().Be(JsonValueKind.Array);
        linksetElement.GetArrayLength().Should().BeGreaterThan(0);

        // Get first linkset entry
        var firstItem = linksetElement[0];
        var firstRelation = firstItem.EnumerateObject().First();
        var firstLink = firstRelation.Value[0];
        firstLink.TryGetProperty("title", out var titleElement).Should().BeTrue();
        titleElement.GetString().Should()
            .Be("Dal Giardino Medicinal Compound 50 x 200mg Capsules page on dalgiardino.com");

        // Test with application/linkset+json
        var headers2 = new Dictionary<string, string>
        {
            ["Accept"] = "application/linkset+json"
        };
        var response2 = await ResolveAsync("/01/09506000134376", headers2);

        response2.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Language and Linktype Negotiation Tests

    [Fact]
    public async Task Test11_LinktypeAndLanguageNegotiation_ShouldReturnCorrectLink()
    {
        // Ensure data is created
        await Test02_CreateEntries_ShouldReturn201();

        var linktypes = new[] { "gs1:hasRetailers", "gs1:pip", "gs1:recipeInfo", "gs1:sustainabilityInfo" };
        var languages = new[] { "en", "es", "vi", "ja" };

        foreach (var linktype in linktypes)
        {
            foreach (var language in languages)
            {
                var headers = new Dictionary<string, string>
                {
                    ["Accept-Language"] = language
                };

                var response = await ResolveAsync($"/01/09506000134352?linktype={linktype}", headers);

                response.StatusCode.Should().Be(HttpStatusCode.RedirectKeepVerb); // 307
                response.Headers.Location.Should().NotBeNull();

                var location = response.Headers.Location!.ToString();
                location.Should().Contain($"test_lt={linktype}");
                location.Should().Contain($"test_lang={language}");
            }
        }
    }

    [Fact]
    public async Task Test12_AcceptLanguageHeaderParsing_ShouldMatchMostPreferred()
    {
        // Ensure data is created
        await Test02_CreateEntries_ShouldReturn201();

        var testCases = new[]
        {
            new { AcceptLanguage = "en-GB,en;q=0.9,en-US;q=0.8,en-IE;q=0.7", ExpectedRegister = "en-GB" },
            new { AcceptLanguage = "en,en-US;q=0.8,en-IE;q=0.7", ExpectedRegister = "en-GB" },
            new { AcceptLanguage = "en-US,en;q=0.9,en-GB;q=0.8,en-IE;q=0.7", ExpectedRegister = "en-US" },
            new { AcceptLanguage = "en-IE;q=0.9,en;q=0.8,en-GB;q=0.7,en-US;q=0.6", ExpectedRegister = "en-IE" },
            new { AcceptLanguage = "fr-BE,fr-FR;q=0.8,fr;q=0.7", ExpectedRegister = "non-English" }
        };

        foreach (var testCase in testCases)
        {
            var headers = new Dictionary<string, string>
            {
                ["Accept-Language"] = testCase.AcceptLanguage
            };

            var response = await ResolveAsync("/01/09506000134376?linktype=gs1:registerProduct", headers);

            response.StatusCode.Should().Be(HttpStatusCode.RedirectKeepVerb); // 307
            response.Headers.Location.Should().NotBeNull();

            var location = response.Headers.Location!.ToString();
            location.Should().Contain($"register={testCase.ExpectedRegister}");
        }
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task Test13_BadGS1DigitalLink_ShouldReturn400()
    {
        var response = await ResolveAsync("/01/09506000134376/badrequest");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest); // 400
    }

    [Fact]
    public async Task Test14_UnavailableLinktype_ShouldReturn404()
    {
        // Ensure data is created
        await Test02_CreateEntries_ShouldReturn201();

        var response = await ResolveAsync("/01/09506000134376?linktype=gs1:safetyInfo");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound); // 404
    }

    [Fact]
    public async Task Test15_MultipleLinks_ShouldReturn300()
    {
        // Ensure data is created
        await Test02_CreateEntries_ShouldReturn201();

        var response = await ResolveAsync("/01/09506000134376/10/LOT01?linktype=gs1:certificationInfo");

        response.StatusCode.Should().Be(HttpStatusCode.Ambiguous); // 300

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content, JsonOptions);

        result.TryGetProperty("linkset", out var linksetElement).Should().BeTrue();
        linksetElement.ValueKind.Should().Be(JsonValueKind.Array);
        linksetElement.GetArrayLength().Should().Be(3);

        // Verify hrefs
        var hrefs = linksetElement.EnumerateArray()
            .Select(item => item.GetProperty("href").GetString())
            .ToList();

        hrefs.Should().Contain(href => href!.Contains("certificate_1?lot=LOT01"));
        hrefs.Should().Contain(href => href!.Contains("certificate_2?lot=LOT01"));
        hrefs.Should().Contain(href => href!.Contains("certificate_3?lot=LOT01"));
    }

    #endregion

    #region GIAI (Asset) Tests

    [Fact]
    public async Task Test16_ResolveFixedAsset_ShouldReturn307()
    {
        // Ensure data is created
        await Test02_CreateEntries_ShouldReturn201();

        var response = await ResolveAsync("/8004/0950600013430000001");

        response.StatusCode.Should().Be(HttpStatusCode.RedirectKeepVerb); // 307
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Be("https://dalgiardino.com/medicinal-compound/assets/8004/0950600013430000001.html");
    }

    [Fact]
    public async Task Test17_ResolveVariableAsset_ShouldReturn307WithTemplate()
    {
        // Ensure data is created
        await Test02_CreateEntries_ShouldReturn201();

        var response = await ResolveAsync("/8004/095060001343999999");

        response.StatusCode.Should().Be(HttpStatusCode.RedirectKeepVerb); // 307
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain("https://dalgiardino.com/medicinal-compound/assets");
        response.Headers.Location!.ToString().Should().Contain("giai=095060001343999999");
    }

    #endregion

    #region SSCC (Logistic Unit) Tests

    [Fact]
    public async Task Test18_ResolveBasicSscc_ShouldReturn307()
    {
        // Ensure data is created
        await Test02_CreateEntries_ShouldReturn201();

        var response = await ResolveAsync("/00/106141412345678908");

        response.StatusCode.Should().Be(HttpStatusCode.RedirectKeepVerb); // 307
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Be("https://dalgiardino.com/logistics/sscc/106141412345678908");
    }

    [Fact]
    public async Task Test19_ResolveSsccWithLinktype_ShouldReturn307()
    {
        // Ensure data is created
        await Test02_CreateEntries_ShouldReturn201();

        var response = await ResolveAsync("/00/106141412345678908?linktype=gs1:traceability");

        response.StatusCode.Should().Be(HttpStatusCode.RedirectKeepVerb); // 307
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Be("https://dalgiardino.com/logistics/trace/106141412345678908");
    }

    [Fact]
    public async Task Test20_ResolveSsccWithLanguageNegotiation_ShouldReturnCorrectLanguage()
    {
        // Ensure data is created
        await Test02_CreateEntries_ShouldReturn201();

        var headers = new Dictionary<string, string>
        {
            ["Accept-Language"] = "es"
        };

        var response = await ResolveAsync("/00/106141412345678908", headers);

        response.StatusCode.Should().Be(HttpStatusCode.RedirectKeepVerb); // 307
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Be("https://dalgiardino.com/logistics/sscc/106141412345678908/es");
    }

    [Fact]
    public async Task Test21_ResolveSsccWithDefaultTraceability_ShouldReturn307()
    {
        // Ensure data is created
        await Test02_CreateEntries_ShouldReturn201();

        var response = await ResolveAsync("/00/095060001343000014");

        response.StatusCode.Should().Be(HttpStatusCode.RedirectKeepVerb); // 307
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Be("https://dalgiardino.com/shipments/095060001343000014/trace");
    }

    [Fact]
    public async Task Test22_ResolveSsccWithMultipleTraceabilityLinks_ShouldReturn300()
    {
        // Ensure data is created
        await Test02_CreateEntries_ShouldReturn201();

        var response = await ResolveAsync("/00/095060001343000014?linktype=gs1:traceability");

        response.StatusCode.Should().Be(HttpStatusCode.Ambiguous); // 300
        response.Headers.Location.Should().BeNull();

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content, JsonOptions);

        result.TryGetProperty("linkset", out var linksetElement).Should().BeTrue();
        linksetElement.ValueKind.Should().Be(JsonValueKind.Array);
        linksetElement.GetArrayLength().Should().Be(2);

        // Verify hrefs for both traceability links
        var hrefs = linksetElement.EnumerateArray()
            .Select(item => item.GetProperty("href").GetString())
            .ToList();

        hrefs.Should().Contain("https://dalgiardino.com/shipments/095060001343000014/trace");
        hrefs.Should().Contain("https://dalgiardino.com/shipments/095060001343000014/trace/es");
    }

    [Fact]
    public async Task Test23_GetSsccLinksetAsJson_ShouldReturn200WithLinkset()
    {
        // Ensure data is created
        await Test02_CreateEntries_ShouldReturn201();

        var headers = new Dictionary<string, string>
        {
            ["Accept"] = "application/json"
        };
        var response = await ResolveAsync("/00/106141412345678908", headers);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content, JsonOptions);

        result.TryGetProperty("linkset", out var linksetElement).Should().BeTrue();
        linksetElement.ValueKind.Should().Be(JsonValueKind.Array);
        linksetElement.GetArrayLength().Should().BeGreaterThan(0);

        var firstLinkset = linksetElement[0];
        firstLinkset.ValueKind.Should().Be(JsonValueKind.Object);

        firstLinkset.EnumerateObject()
            .Any(p => p.Value.ValueKind == JsonValueKind.Array)
            .Should().BeTrue();

        result.TryGetProperty("@id", out var idElement).Should().BeTrue();
        idElement.GetString().Should().Contain("/00/106141412345678908");
    }

    [Fact]
    public async Task Test24_ResolveQualifiedSscc_ShouldReturn307WithLotNumber()
    {
        // Ensure data is created
        await Test02_CreateEntries_ShouldReturn201();

        // Test SSCC with lot number qualifier: /00/100000000000000000/10/LOT01
        var response = await ResolveAsync("/00/100000000000000007/10/LOT01");

        response.StatusCode.Should().Be(HttpStatusCode.RedirectKeepVerb); // 307
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain("https://dalgiardino.com/logistics/sscc/100000000000000007");
        response.Headers.Location!.ToString().Should().Contain("lot=LOT01");
    }

    [Fact]
    public async Task Test25_ResolveQualifiedSsccWithTraceability_ShouldReturn300()
    {
        // Ensure data is created
        await Test02_CreateEntries_ShouldReturn201();

        // Test SSCC with lot number qualifier and traceability linktype
        var response = await ResolveAsync("/00/100000000000000007/10/LOT01?linktype=gs1:traceability");

        response.StatusCode.Should().Be(HttpStatusCode.Ambiguous); // 300
        response.Headers.Location.Should().BeNull();

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content, JsonOptions);

        result.TryGetProperty("linkset", out var linksetElement).Should().BeTrue();
        linksetElement.ValueKind.Should().Be(JsonValueKind.Array);
        linksetElement.GetArrayLength().Should().Be(2);

        // Verify hrefs for both traceability links (English and Spanish)
        var hrefs = linksetElement.EnumerateArray()
            .Select(item => item.GetProperty("href").GetString())
            .ToList();

        hrefs.Should().Contain("https://dalgiardino.com/logistics/trace/100000000000000007?lot=LOT01");
        hrefs.Should().Contain("https://dalgiardino.com/logistics/trace/100000000000000007/es?lot=LOT01");
    }

    [Fact]
    public async Task Test26_GetQualifiedSsccLinksetAsJson_ShouldReturn200WithLinkset()
    {
        // Ensure data is created
        await Test02_CreateEntries_ShouldReturn201();

        var headers = new Dictionary<string, string>
        {
            ["Accept"] = "application/json"
        };
        var response = await ResolveAsync("/00/100000000000000007/10/LOT01", headers);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content, JsonOptions);

        //result.TryGetProperty("data", out var dataElement).Should().BeTrue();
        //dataElement.ValueKind.Should().Be(JsonValueKind.Array);
        //dataElement.GetArrayLength().Should().BeGreaterThan(0);

        //var firstItem = dataElement[0];
        //firstItem.TryGetProperty("anchor", out var anchorElement).Should().BeTrue();
        //anchorElement.GetString().Should().Contain("/00/100000000000000007");

        //firstItem.TryGetProperty("itemDescription", out var descElement).Should().BeTrue();
        //descElement.GetString().Should().Be("Dal Giardino Pallet Shipment - Qualified SSCC with Lot Number");

        //// Verify qualifiers are present
        //firstItem.TryGetProperty("qualifiers", out var qualifiersElement).Should().BeTrue();
        //qualifiersElement.ValueKind.Should().Be(JsonValueKind.Array);
        //qualifiersElement.GetArrayLength().Should().Be(1);

        //var qualifier = qualifiersElement[0];
        //qualifier.TryGetProperty("10", out var lotElement).Should().BeTrue();
        //lotElement.GetString().Should().Be("{lotnumber}");

        // Verify Digital Link identity
        result.TryGetProperty("@id", out var idElement).Should().BeTrue();
        idElement.GetString().Should().Contain("/00/100000000000000007");

        result.TryGetProperty("gs1:elementStrings", out var elementStrings).Should().BeTrue();
        elementStrings.ValueKind.Should().Be(JsonValueKind.Array);
        elementStrings.GetArrayLength().Should().BeGreaterThan(0);

        result.TryGetProperty("linkset", out var linksetElement).Should().BeTrue();
        linksetElement.ValueKind.Should().Be(JsonValueKind.Array);
        linksetElement.GetArrayLength().Should().BeGreaterThan(0);
    }

    #endregion

    #region Helper Methods

    private async Task<HttpResponseMessage> CreateEntryAsync(DataEntryV3Document entry)
    {
        var json = JsonSerializer.Serialize(entry, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/new/single")
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "secret");

        return await _dataEntryClient.SendAsync(request);
    }

    private async Task<HttpResponseMessage> DeleteEntryAsync(string anchor)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/{anchor}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "secret");

        return await _dataEntryClient.SendAsync(request);
    }

    private async Task<HttpResponseMessage> GetEntryAsync(string anchor)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/{anchor}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "secret");

        return await _dataEntryClient.SendAsync(request);
    }

    private async Task<HttpResponseMessage> ResolveAsync(string path, Dictionary<string, string>? headers = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);

        if (headers != null)
        {
            foreach (var header in headers)
            {
                if (header.Key.Equals("Accept", StringComparison.OrdinalIgnoreCase))
                {
                    request.Headers.Accept.Clear();
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(header.Value));
                }
                else if (header.Key.Equals("Accept-Language", StringComparison.OrdinalIgnoreCase))
                {
                    request.Headers.AcceptLanguage.Clear();
                    var languages = header.Value.Split(',');
                    foreach (var lang in languages)
                    {
                        var parts = lang.Trim().Split(';');
                        var language = parts[0].Trim();
                        var quality = 1.0;
                        if (parts.Length > 1 && parts[1].StartsWith("q="))
                        {
                            double.TryParse(parts[1].Substring(2), out quality);
                        }
                        request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue(language, quality));
                    }
                }
                else
                {
                    request.Headers.Add(header.Key, header.Value);
                }
            }
        }

        return await _webResolverClient.SendAsync(request);
    }

    /// <summary>
    /// Helper method to match qualifiers between two documents.
    /// Returns true if both are null/empty or if they contain the same qualifier dictionaries.
    /// </summary>
    private bool MatchQualifiers(List<Dictionary<string, string>>? qualifiers1, List<Dictionary<string, string>>? qualifiers2)
    {
        // Both null or empty - match
        if ((qualifiers1 == null || qualifiers1.Count == 0) && (qualifiers2 == null || qualifiers2.Count == 0))
        {
            return true;
        }

        // One is null/empty and the other isn't - no match
        if ((qualifiers1 == null || qualifiers1.Count == 0) || (qualifiers2 == null || qualifiers2.Count == 0))
        {
            return false;
        }

        // Both have qualifiers - check if they match
        if (qualifiers1.Count != qualifiers2.Count)
        {
            return false;
        }

        for (int i = 0; i < qualifiers1.Count; i++)
        {
            var q1 = qualifiers1[i];
            var q2 = qualifiers2[i];

            if (q1.Count != q2.Count)
            {
                return false;
            }

            foreach (var kvp in q1)
            {
                if (!q2.TryGetValue(kvp.Key, out var value) || value != kvp.Value)
                {
                    return false;
                }
            }
        }

        return true;
    }

    #endregion
}

