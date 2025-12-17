using GS1Resolver.Shared.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace GS1Resolver.Shared.Services;

public class LinksetFormatterService : ILinksetFormatterService
{
    private readonly ILogger<LinksetFormatterService> _logger;

    public LinksetFormatterService(ILogger<LinksetFormatterService> logger)
    {
        _logger = logger;
    }

    public object FormatLinksetForExternalUse(
        ResolverDocument document,
        List<LinksetDataItem> matchedItems,
        string identifier,
        string fqdn)
    {
        try
        {
            // Extract AI code and value from identifier (e.g., "/01/09521234543213")
            var identifierParts = identifier.TrimStart('/').Split('/', 2);
            var aiCode = identifierParts.Length > 0 ? identifierParts[0] : "";
            var aiValue = identifierParts.Length > 1 ? identifierParts[1] : "";

            // Build JSON-LD context
            var context = new Dictionary<string, object>
            {
                { "gs1", "https://gs1.org/voc/" },
                { "schema", "https://schema.org/" },
                { "linkset", "https://www.w3.org/ns/linkset#" }
            };

            // Add GTIN properties if AI code is 01
            if (aiCode == "01")
            {
                context["gtin"] = new Dictionary<string, string>
                {
                    { "@id", "gs1:gtin" },
                    { "@type", "@id" }
                };
            }

            // Build the main response object
            var response = new Dictionary<string, object>
            {
                { "@context", context },
                { "@id", $"https://{fqdn}{identifier}" },
                { "@type", "gs1:DigitalLink" },
                { "gs1:elementStrings", new List<string> { identifier } }
            };

            // Add GTIN value if applicable
            if (aiCode == "01")
            {
                response["gtin"] = aiValue;
            }

            // Process linkset items
            var processedLinkset = ProcessLinksetItems(matchedItems, fqdn);
            response["linkset"] = processedLinkset;

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error formatting linkset for external use");
            throw;
        }
    }

    public string GenerateLinkHeader(
        List<LinksetDataItem> linksetItems,
        string identifier,
        string fqdn)
    {
        return $"<https://{fqdn}{identifier}?linkType=linkset>; rel=\"application/linkset\"; type=\"application/linkset+json\"; title=\"Linkset for {identifier}\"";
    }

    private List<Dictionary<string, object>> ProcessLinksetItems(
        List<LinksetDataItem> items,
        string fqdn)
    {
        var processedItems = new List<Dictionary<string, object>>();

        foreach (var item in items)
        {
            var processedItem = new Dictionary<string, object>();

            // Process linkset entries organized by GS1 vocabulary terms
            if (item.Linkset?.LinkTypes != null && item.Linkset.LinkTypes.Any())
            {
                foreach (var linkTypeGroup in item.Linkset.LinkTypes)
                {
                    var linkType = linkTypeGroup.Key; // e.g., "https://gs1.org/voc/pip"
                    var entries = linkTypeGroup.Value;

                    var processedEntries = new List<Dictionary<string, object>>();

                    foreach (var entry in entries)
                    {
                        var processedEntry = new Dictionary<string, object>();

                        if (!string.IsNullOrWhiteSpace(entry.Href))
                        {
                            // Normalize href to fully qualified URL
                            var normalizedHref = entry.Href;

                            // If href starts with / or doesn't contain ://, prepend https://fqdn
                            if (normalizedHref.StartsWith("/") || !normalizedHref.Contains("://"))
                            {
                                // Remove leading / if present to avoid double slashes
                                var path = normalizedHref.TrimStart('/');
                                normalizedHref = $"https://{fqdn}/{path}";
                            }
                            // Otherwise, it's already an absolute URL (contains ://)

                            processedEntry["href"] = normalizedHref;
                        }

                        if (!string.IsNullOrWhiteSpace(entry.Type))
                        {
                            processedEntry["type"] = entry.Type;
                        }

                        if (entry.Hreflang != null && entry.Hreflang.Any())
                        {
                            // Remove "und" from hreflang (internal-only value)
                            var filteredHreflang = entry.Hreflang
                                .Where(lang => !lang.Equals("und", StringComparison.OrdinalIgnoreCase))
                                .ToList();

                            if (filteredHreflang.Any())
                            {
                                processedEntry["hreflang"] = filteredHreflang;
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(entry.Title))
                        {
                            processedEntry["title"] = entry.Title;
                        }

                        if (entry.Context != null && entry.Context.Any())
                        {
                            processedEntry["context"] = entry.Context;
                        }

                        processedEntries.Add(processedEntry);
                    }

                    if (processedEntries.Any())
                    {
                        processedItem[linkType] = processedEntries;
                    }
                }
            }

            if (processedItem.Any())
            {
                processedItems.Add(processedItem);
            }
        }

        return processedItems;
    }
}
