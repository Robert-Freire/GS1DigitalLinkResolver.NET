using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Swashbuckle.AspNetCore.Annotations;

namespace GS1Resolver.Shared.Models;

/// <summary>
/// Models for v2 to v3 migration support. These are used only for the one-time migration endpoint.
/// </summary>

/// <summary>
/// Legacy v2 document format for migration purposes.
/// </summary>
[SwaggerSchema(Description = "Legacy v2 document format for one-time migration to v3 format.")]
public class DataEntryV2Document
{
    /// <summary>
    /// The GS1 Application Identifier type (e.g., "01" for GTIN, "414" for GLN).
    /// </summary>
    [JsonPropertyName("identificationKeyType")]
    [Required(ErrorMessage = "IdentificationKeyType is required")]
    [SwaggerSchema(Description = "GS1 Application Identifier type (e.g., '01', '414').")]
    public string IdentificationKeyType { get; set; } = string.Empty;

    /// <summary>
    /// The GS1 identifier value (e.g., GTIN, GLN).
    /// </summary>
    [JsonPropertyName("identificationKey")]
    [Required(ErrorMessage = "IdentificationKey is required")]
    [SwaggerSchema(Description = "GS1 identifier value (e.g., GTIN, GLN).")]
    public string IdentificationKey { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of the item.
    /// </summary>
    [JsonPropertyName("itemDescription")]
    [SwaggerSchema(Description = "Optional description of the item.")]
    public string? ItemDescription { get; set; }

    /// <summary>
    /// Optional qualifier path (e.g., "/21/12345/10/ABC").
    /// </summary>
    [JsonPropertyName("qualifierPath")]
    [SwaggerSchema(Description = "Optional qualifier path (e.g., '/21/12345/10/ABC').")]
    public string? QualifierPath { get; set; }

    /// <summary>
    /// Indicates if the document is active (default: true).
    /// </summary>
    [JsonPropertyName("active")]
    [SwaggerSchema(Description = "Indicates if the document is active.")]
    public bool Active { get; set; } = true;

    /// <summary>
    /// List of response items (links) associated with this document.
    /// </summary>
    [JsonPropertyName("responses")]
    [SwaggerSchema(Description = "List of response items (links) for this document.")]
    public List<ResponseItemV2>? Responses { get; set; }
}

/// <summary>
/// Legacy v2 response item for migration purposes.
/// </summary>
[SwaggerSchema(Description = "Legacy v2 response item representing a link with metadata.")]
public class ResponseItemV2
{
    /// <summary>
    /// The link type (e.g., "pip", "certificationInfo").
    /// </summary>
    [JsonPropertyName("linkType")]
    [Required(ErrorMessage = "LinkType is required")]
    [SwaggerSchema(Description = "Link type (e.g., 'pip', 'certificationInfo').")]
    public string LinkType { get; set; } = string.Empty;

    /// <summary>
    /// IANA language code (e.g., "en", "fr").
    /// </summary>
    [JsonPropertyName("ianaLanguage")]
    [SwaggerSchema(Description = "IANA language code (e.g., 'en', 'fr').")]
    public string? IanaLanguage { get; set; }

    /// <summary>
    /// Context of the link (e.g., application context).
    /// </summary>
    [JsonPropertyName("context")]
    [SwaggerSchema(Description = "Context of the link.")]
    public string? Context { get; set; }

    /// <summary>
    /// MIME type of the linked resource.
    /// </summary>
    [JsonPropertyName("mimeType")]
    [SwaggerSchema(Description = "MIME type of the linked resource.")]
    public string? MimeType { get; set; }

    /// <summary>
    /// Human-readable title for the link.
    /// </summary>
    [JsonPropertyName("linkTitle")]
    [Required(ErrorMessage = "LinkTitle is required")]
    [SwaggerSchema(Description = "Human-readable title for the link.")]
    public string LinkTitle { get; set; } = string.Empty;

    /// <summary>
    /// Target URL for the link.
    /// </summary>
    [JsonPropertyName("targetUrl")]
    [Required(ErrorMessage = "TargetUrl is required")]
    [Url(ErrorMessage = "TargetUrl must be a valid URL")]
    [SwaggerSchema(Description = "Target URL for the link.")]
    public string TargetUrl { get; set; } = string.Empty;

    /// <summary>
    /// Indicates if this is the default link type.
    /// </summary>
    [JsonPropertyName("defaultLinkType")]
    [SwaggerSchema(Description = "Indicates if this is the default link type.")]
    public bool DefaultLinkType { get; set; } = false;

    /// <summary>
    /// Indicates if this is the default IANA language.
    /// </summary>
    [JsonPropertyName("defaultIanaLanguage")]
    [SwaggerSchema(Description = "Indicates if this is the default IANA language.")]
    public bool DefaultIanaLanguage { get; set; } = false;

    /// <summary>
    /// Indicates if this is the default context.
    /// </summary>
    [JsonPropertyName("defaultContext")]
    [SwaggerSchema(Description = "Indicates if this is the default context.")]
    public bool DefaultContext { get; set; } = false;

    /// <summary>
    /// Indicates if this is the default MIME type.
    /// </summary>
    [JsonPropertyName("defaultMimeType")]
    [SwaggerSchema(Description = "Indicates if this is the default MIME type.")]
    public bool DefaultMimeType { get; set; } = false;

    /// <summary>
    /// Forward Query String (FWQS) flag.
    /// </summary>
    [JsonPropertyName("fwqs")]
    [SwaggerSchema(Description = "Forward Query String (FWQS) flag.")]
    public bool Fwqs { get; set; } = false;

    /// <summary>
    /// Indicates if this response item is active.
    /// </summary>
    [JsonPropertyName("active")]
    [SwaggerSchema(Description = "Indicates if this response item is active.")]
    public bool Active { get; set; } = true;
}

/// <summary>
/// Result object for document creation operations.
/// </summary>
public class CreateResult
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
