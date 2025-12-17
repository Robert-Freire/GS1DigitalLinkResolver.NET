using System.Text.Json;
using GS1Resolver.Shared.Models;

namespace GS1Resolver.Shared.Tests.TestData;

public class TestDataEntry
{
    public List<DataEntryV3Document> Documents { get; set; } = new();
    public string FileName { get; set; } = string.Empty;
}

public static class TestDataLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static List<TestDataEntry> LoadTestFiles(string testDirectory)
    {
        var testDataEntries = new List<TestDataEntry>();

        if (!Directory.Exists(testDirectory))
        {
            throw new DirectoryNotFoundException($"Test data directory not found: {testDirectory}");
        }

        var jsonFiles = Directory.GetFiles(testDirectory, "test_*.json");

        foreach (var file in jsonFiles)
        {
            var fileName = Path.GetFileName(file);
            var jsonContent = File.ReadAllText(file);

            try
            {
                // Try to parse as array first
                var documentsArray = JsonSerializer.Deserialize<List<DataEntryV3Document>>(jsonContent, JsonOptions);
                if (documentsArray != null && documentsArray.Count > 0)
                {
                    testDataEntries.Add(new TestDataEntry
                    {
                        Documents = documentsArray,
                        FileName = fileName
                    });
                    continue;
                }
            }
            catch (JsonException)
            {
                // Not an array, try single object
            }

            try
            {
                // Try to parse as single object
                var singleDocument = JsonSerializer.Deserialize<DataEntryV3Document>(jsonContent, JsonOptions);
                if (singleDocument != null)
                {
                    testDataEntries.Add(new TestDataEntry
                    {
                        Documents = new List<DataEntryV3Document> { singleDocument },
                        FileName = fileName
                    });
                }
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Failed to parse test file {fileName}: {ex.Message}", ex);
            }
        }

        return testDataEntries;
    }

    public static string ExtractAnchor(DataEntryV3Document document)
    {
        // Extract anchor from the Anchor property, trimming the leading slash for use in request URLs
        var anchor = document.Anchor.TrimStart('/');
        return anchor;
    }
}
