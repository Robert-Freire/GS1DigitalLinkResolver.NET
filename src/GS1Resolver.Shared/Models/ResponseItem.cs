using System.Text.Json.Serialization;

namespace GS1Resolver.Shared.Models;

public class ResponseItem
{
    [JsonPropertyName("linktype")]
    public string? Linktype { get; set; }

    [JsonPropertyName("ianaLanguage")]
    public string? IanaLanguage { get; set; }

    [JsonPropertyName("context")]
    public string? Context { get; set; }

    [JsonPropertyName("mimeType")]
    public string? MimeType { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("href")]
    public string? Href { get; set; }

    [JsonPropertyName("defaultLinktype")]
    public bool? DefaultLinktype { get; set; }

    [JsonPropertyName("fwqs")]
    public bool? Fwqs { get; set; }

    [JsonPropertyName("active")]
    public bool? Active { get; set; }
}
