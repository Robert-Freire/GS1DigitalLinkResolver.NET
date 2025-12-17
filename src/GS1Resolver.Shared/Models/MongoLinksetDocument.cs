using System.Text.Json.Serialization;

namespace GS1Resolver.Shared.Models;

/// <summary>
/// Internal document format for MongoDB/Cosmos DB storage.
/// Matches Python Mongo schema: id, defaultLinktype, data[]
/// No top-level anchor or itemDescription (those are in linkset objects within data items).
/// </summary>
public class MongoLinksetDocument
{
    /// <summary>
    /// Document identifier derived from the anchor path.
    /// Example: "01_09506000134376" for anchor "/01/09506000134376"
    /// </summary>
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Default linktype to use when no specific linktype is requested.
    /// Example: "gs1:pip"
    /// </summary>
    [JsonPropertyName("defaultLinktype")]
    public string? DefaultLinktype { get; set; }

    /// <summary>
    /// Array of data items, each containing qualifiers and linksets.
    /// Each linkset contains itemDescription and link types.
    /// </summary>
    [JsonPropertyName("data")]
    public List<LinksetDataItem> Data { get; set; } = new();
}
