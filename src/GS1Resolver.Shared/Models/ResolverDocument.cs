using System.Text.Json;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace GS1Resolver.Shared.Models;

/// <summary>
/// Represents a resolver document in Cosmos DB.
/// Matches the Python Mongo schema: id, defaultLinktype, data[]
/// No top-level anchor or itemDescription (those are in linkset objects).
/// </summary>
public class ResolverDocument
{
    [JsonPropertyName("id")]  // For System.Text.Json (ASP.NET Core)
    [JsonProperty("id")]      // For Newtonsoft.Json (Cosmos SDK)
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("defaultLinktype")]
    [JsonProperty("defaultLinktype")]
    public string? DefaultLinktype { get; set; }

    [JsonPropertyName("data")]
    [JsonProperty("data")]
    public List<LinksetDataItem> Data { get; set; } = new();
}
