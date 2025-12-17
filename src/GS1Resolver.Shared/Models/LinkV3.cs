using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace GS1Resolver.Shared.Models;

/// <summary>
/// Represents a single link in v3 format following GS1 Digital Link specifications.
/// </summary>
public class LinkV3
{
    /// <summary>
    /// The linktype identifier (e.g., "gs1:hasRetailers", "gs1:pip").
    /// </summary>
    [Required(ErrorMessage = "Linktype is required")]
    [MinLength(1, ErrorMessage = "Linktype cannot be empty")]
    [JsonPropertyName("linktype")]
    public string Linktype { get; set; } = string.Empty;

    /// <summary>
    /// The URL/URI for this link.
    /// </summary>
    [Required(ErrorMessage = "Href is required")]
    [MinLength(1, ErrorMessage = "Href cannot be empty")]
    [Url(ErrorMessage = "Href must be a valid URL")]
    [JsonPropertyName("href")]
    public string Href { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable title for the link.
    /// </summary>
    [Required(ErrorMessage = "Title is required")]
    [MinLength(1, ErrorMessage = "Title cannot be empty")]
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// MIME type of the linked resource (e.g., "text/html", "application/pdf").
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>
    /// List of language codes (e.g., ["en", "fr"]) for the linked resource.
    /// </summary>
    [JsonPropertyName("hreflang")]
    public List<string>? Hreflang { get; set; }

    /// <summary>
    /// List of context identifiers for when this link should be used.
    /// </summary>
    [JsonPropertyName("context")]
    public List<string>? Context { get; set; }
}
