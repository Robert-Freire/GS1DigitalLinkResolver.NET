using GS1Resolver.Shared.Models;
using GS1Resolver.Shared.Services;

namespace GS1Resolver.Shared.Tests.Mocks;

/// <summary>
/// Mock implementation of IGS1ToolkitService for testing when the GS1 toolkit is unavailable.
/// Provides basic validation and processing for common GS1 Digital Link patterns.
/// </summary>
public class MockGS1ToolkitService : IGS1ToolkitService
{
    public Task<bool> TestDigitalLinkSyntaxAsync(string urlPath)
    {
        // Basic mock syntax test - accepts paths starting with common AI codes
        if (string.IsNullOrWhiteSpace(urlPath))
        {
            return Task.FromResult(false);
        }

        // Accept paths that look like GS1 Digital Links
        var validPrefixes = new[] { "/01/", "/8004/", "/00/", "/414/" };
        var isValid = validPrefixes.Any(prefix => urlPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(isValid);
    }

    public Task<GS1ToolkitResult> UncompressDigitalLinkAsync(string compressedLink)
    {
        // Mock uncompression - return a simple structure
        if (string.IsNullOrWhiteSpace(compressedLink))
        {
            return Task.FromResult(new GS1ToolkitResult
            {
                Success = false,
                Error = "Compressed link cannot be empty"
            });
        }

        // Handle the specific test case: /ARFKk4XB0CDKWcnpq -> /01/09506000134376/10/LOT01
        if (compressedLink == "/ARFKk4XB0CDKWcnpq")
        {
            return Task.FromResult(new GS1ToolkitResult
            {
                Success = true,
                Identifiers = new List<Dictionary<string, string>>
                {
                    new Dictionary<string, string> { { "01", "09506000134376" } }
                },
                Qualifiers = new List<Dictionary<string, string>>
                {
                    new Dictionary<string, string> { { "10", "LOT01" } }
                }
            });
        }

        return Task.FromResult(new GS1ToolkitResult
        {
            Success = true,
            Identifiers = new List<Dictionary<string, string>>
            {
                new Dictionary<string, string> { { "01", "09506000134376" } }
            },
            Qualifiers = new List<Dictionary<string, string>>()
        });
    }

    public Task<GS1ToolkitResult> CompressDigitalLinkAsync(string uncompressedLink)
    {
        // Mock compression - return a mock compressed path
        if (string.IsNullOrWhiteSpace(uncompressedLink))
        {
            return Task.FromResult(new GS1ToolkitResult
            {
                Success = false,
                Error = "Uncompressed link cannot be empty"
            });
        }

        // Handle the specific test case: /01/09506000134376/10/LOT01 -> /ARFKk4XB0CDKWcnpq
        // The link can come with or without a domain prefix
        if (uncompressedLink == "/01/09506000134376/10/LOT01" ||
            uncompressedLink.EndsWith("/01/09506000134376/10/LOT01"))
        {
            return Task.FromResult(new GS1ToolkitResult
            {
                Success = true,
                Compressed = "/ARFKk4XB0CDKWcnpq"
            });
        }

        // Generate a simple mock compressed link based on hash
        var mockCompressed = $"/MOCK{Math.Abs(uncompressedLink.GetHashCode()) % 10000}";

        return Task.FromResult(new GS1ToolkitResult
        {
            Success = true,
            Compressed = mockCompressed
        });
    }

    public Task<GS1ToolkitResult> AnalyzeDigitalLinkAsync(string digitalLink)
    {
        // Mock analysis - extract basic structure
        if (string.IsNullOrWhiteSpace(digitalLink))
        {
            return Task.FromResult(new GS1ToolkitResult
            {
                Success = false,
                Error = "Digital link cannot be empty"
            });
        }

        return Task.FromResult(new GS1ToolkitResult
        {
            Success = true,
            Identifiers = new List<Dictionary<string, string>>
            {
                new Dictionary<string, string> { { "01", "00000000000000" } }
            },
            Qualifiers = new List<Dictionary<string, string>>(),
            DataAttributes = new List<Dictionary<string, string>>()
        });
    }
}
