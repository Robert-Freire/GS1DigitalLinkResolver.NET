using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace GS1Resolver.Shared.Models;

/// <summary>
/// Represents a single entry within a linkset array in the internal storage format.
/// </summary>
public class LinksetEntry
{
    /// <summary>
    /// The URL/URI for this link.
    /// </summary>
    [JsonPropertyName("href")]
    [JsonProperty("href")]
    public string Href { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable title for the link.
    /// </summary>
    [JsonPropertyName("title")]
    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// MIME type of the linked resource (e.g., "text/html", "application/pdf").
    /// </summary>
    [JsonPropertyName("type")]
    [JsonProperty("type")]
    public string? Type { get; set; }

    /// <summary>
    /// List of language codes (e.g., ["en", "fr"]) for the linked resource.
    /// </summary>
    [JsonPropertyName("hreflang")]
    [JsonProperty("hreflang")]
    public List<string>? Hreflang { get; set; }

    /// <summary>
    /// List of context identifiers for when this link should be used.
    /// </summary>
    [JsonPropertyName("context")]
    [JsonProperty("context")]
    public List<string>? Context { get; set; }
}
