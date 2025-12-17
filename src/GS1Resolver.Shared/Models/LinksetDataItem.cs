using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace GS1Resolver.Shared.Models;

/// <summary>
/// Represents a single data item in the internal linkset storage format.
/// Each data item contains qualifiers and a linkset object.
/// Matches the Python Mongo schema structure.
/// </summary>
public class LinksetDataItem
{
    /// <summary>
    /// List of qualifier sets. Each set is a dictionary mapping AI codes to values.
    /// Example: [{"21": "{serialnumber}"}]
    /// </summary>
    [JsonPropertyName("qualifiers")]
    [JsonProperty("qualifiers")]
    public List<Dictionary<string, string>> Qualifiers { get; set; } = new();

    /// <summary>
    /// The linkset object containing item description and link types.
    /// Matches Python schema: linkset[0] = { itemDescription, ...linkTypes }
    /// </summary>
    [JsonPropertyName("linkset")]
    [JsonProperty("linkset")]
    public LinksetObject Linkset { get; set; } = new();
}
