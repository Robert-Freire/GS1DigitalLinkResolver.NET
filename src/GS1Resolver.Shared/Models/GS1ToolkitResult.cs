using System.Text.Json.Serialization;

namespace GS1Resolver.Shared.Models;

/// <summary>
/// Represents the result of GS1 toolkit operations (compression, decompression, analysis).
/// Contains structured data extracted from Digital Link URIs including identifiers, qualifiers, and data attributes.
/// </summary>
public class GS1ToolkitResult
{
    /// <summary>
    /// Indicates whether the operation completed successfully.
    /// </summary>
    [JsonPropertyName("SUCCESS")]
    public bool Success { get; set; }

    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    [JsonPropertyName("ERROR")]
    public string? Error { get; set; }

    /// <summary>
    /// GS1 identifiers extracted from the Digital Link (e.g., [{"01": "05392000229648"}]).
    /// Primary keys like GTIN (01), GLN (414), etc.
    /// </summary>
    [JsonPropertyName("identifiers")]
    public List<Dictionary<string, string>>? Identifiers { get; set; }

    /// <summary>
    /// Qualifier key-value pairs (e.g., [{"10": "LOT01"}, {"21": "SER1234"}]).
    /// Additional identifiers like lot/batch (10), serial number (21), expiry date (17), etc.
    /// </summary>
    [JsonPropertyName("qualifiers")]
    public List<Dictionary<string, string>>? Qualifiers { get; set; }

    /// <summary>
    /// Additional data attributes from the Digital Link.
    /// </summary>
    [JsonPropertyName("dataAttributes")]
    public List<Dictionary<string, string>>? DataAttributes { get; set; }

    /// <summary>
    /// Other query parameters not part of standard GS1 elements.
    /// </summary>
    [JsonPropertyName("other")]
    public List<Dictionary<string, string>>? Other { get; set; }

    /// <summary>
    /// Compressed Digital Link URI (populated for compression operations).
    /// Example: "/AQnO2IRCICDKWcnpqQs6QiOu2_A"
    /// </summary>
    [JsonPropertyName("COMPRESSED")]
    public string? Compressed { get; set; }
}
