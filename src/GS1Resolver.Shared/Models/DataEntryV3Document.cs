using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace GS1Resolver.Shared.Models;

/// <summary>
/// Primary input/output model for v3 data entry operations.
/// Represents a single GS1 Digital Link entry with anchor, qualifiers, and links.
/// </summary>
public class DataEntryV3Document
{
    /// <summary>
    /// The anchor path (e.g., "/01/09506000134376" or "/01/09506000134376/21/12345").
    /// Required for identifying the resource.
    /// </summary>
    [Required(ErrorMessage = "Anchor is required")]
    [MinLength(1, ErrorMessage = "Anchor cannot be empty")]
    [JsonPropertyName("anchor")]
    public string Anchor { get; set; } = string.Empty;

    /// <summary>
    /// Optional human-readable description of the item.
    /// </summary>
    [JsonPropertyName("itemDescription")]
    public string? ItemDescription { get; set; }

    /// <summary>
    /// Default linktype to use when no specific linktype is requested.
    /// </summary>
    [JsonPropertyName("defaultLinktype")]
    public string? DefaultLinktype { get; set; }

    /// <summary>
    /// List of qualifier sets. Each set is a dictionary mapping AI codes to values.
    /// Example: [{"21": "{serialnumber}"}, {"10": "{batch}"}]
    /// </summary>
    [JsonPropertyName("qualifiers")]
    public List<Dictionary<string, string>>? Qualifiers { get; set; }

    /// <summary>
    /// List of links associated with this resource.
    /// </summary>
    [Required(ErrorMessage = "Links list is required")]
    [MinLength(1, ErrorMessage = "At least one link is required")]
    [JsonPropertyName("links")]
    public List<LinkV3> Links { get; set; } = new();
}
