using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace GS1Resolver.Shared.Models;

/// <summary>
/// Represents the linkset object containing item description and link types.
/// Matches the Python schema where linkset contains both itemDescription and link types.
/// </summary>
public class LinksetObject
{
    /// <summary>
    /// Optional item description specific to this linkset.
    /// </summary>
    [JsonPropertyName("itemDescription")]
    [JsonProperty("itemDescription")]
    public string? ItemDescription { get; set; }

    /// <summary>
    /// Dictionary of link types organized by GS1 vocabulary terms.
    /// Keys are full GS1 URIs (e.g., "https://gs1.org/voc/pip").
    /// Values are arrays of LinksetEntry objects.
    /// </summary>
    [JsonPropertyName("linkTypes")]
    [JsonProperty("linkTypes")]
    public Dictionary<string, List<LinksetEntry>> LinkTypes { get; set; } = new();
}
