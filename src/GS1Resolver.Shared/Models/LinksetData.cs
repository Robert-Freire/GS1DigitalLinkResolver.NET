using System.Text.Json.Serialization;

namespace GS1Resolver.Shared.Models;

public class LinksetData
{
    [JsonPropertyName("anchor")]
    public string? Anchor { get; set; }

    [JsonPropertyName("itemDescription")]
    public string? ItemDescription { get; set; }

    [JsonPropertyName("qualifiers")]
    public Dictionary<string, string>? Qualifiers { get; set; }

    [JsonPropertyName("responses")]
    public List<ResponseItem>? Responses { get; set; }
}
