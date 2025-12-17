using GS1Resolver.Shared.Configuration;
using GS1Resolver.Shared.Exceptions;
using GS1Resolver.Shared.Models;
using GS1Resolver.Shared.Repositories;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.RegularExpressions;
using ValidationException = System.ComponentModel.DataAnnotations.ValidationException;

namespace GS1Resolver.Shared.Services;

public class WebResolverLogicService : IWebResolverLogicService
{
    private readonly IResolverRepository _repository;
    private readonly IGS1ToolkitService _gs1Toolkit;
    private readonly IContentNegotiationService _contentNegotiation;
    private readonly ILinksetFormatterService _linksetFormatter;
    private readonly ILogger<WebResolverLogicService> _logger;
    private readonly string _fqdn;

    // Serialized identifier AI codes (partial matching support)
    private static readonly HashSet<string> SerializedAiCodes = new() { "8003", "8004", "00" };

    private const string GS1_VOC_BASE = "https://gs1.org/voc/";

    public WebResolverLogicService(
        IResolverRepository repository,
        IGS1ToolkitService gs1Toolkit,
        IContentNegotiationService contentNegotiation,
        ILinksetFormatterService linksetFormatter,
        ILogger<WebResolverLogicService> logger,
        IOptions<FqdnSettings> fqdnSettings)
    {
        _repository = repository;
        _gs1Toolkit = gs1Toolkit;
        _contentNegotiation = contentNegotiation;
        _linksetFormatter = linksetFormatter;
        _logger = logger;
        _fqdn = fqdnSettings.Value.DomainName;
    }

    public async Task<ResolverResponse> ResolveAsync(
        string identifier,
        string? qualifierPath,
        ResolverRequestContext context)
    {
        try
        {
            _logger.LogDebug("Resolving identifier: {Identifier}, qualifiers: {Qualifiers}", identifier, qualifierPath);

            // Validate syntax and fetch document
            var (document, actualIdentifier, templateVariables) = await ValidateAndFetchDocumentAsync(identifier, qualifierPath);

            if (document == null)
            {
                return new ResolverResponse
                {
                    StatusCode = 404,
                    ErrorMessage = "No resolver document found for this identifier"
                };
            }

            // Get linkset data
            var linksetData = document.Data ?? new List<LinksetDataItem>();

            // If qualifiers provided, filter items to only those that match
            if (!string.IsNullOrWhiteSpace(qualifierPath))
            {
                var (filteredItems, qualifierTemplateVars) = await FilterItemsByQualifiersAsync(qualifierPath, linksetData);

                if (!filteredItems.Any())
                {
                    return new ResolverResponse
                    {
                        StatusCode = 404,
                        ErrorMessage = "No matching qualifiers found"
                    };
                }

                // Use only the filtered items that matched qualifiers
                linksetData = filteredItems;

                // Merge template variables from qualifier matching
                if (qualifierTemplateVars != null && qualifierTemplateVars.Any())
                {
                    foreach (var kvp in qualifierTemplateVars)
                    {
                        templateVariables[kvp.Key] = kvp.Value;
                    }
                }
            }
            else
            {
                // No qualifiers in request - filter to only items without qualifiers
                linksetData = linksetData.Where(item =>
                    item.Qualifiers == null ||
                    item.Qualifiers.Count == 0
                ).ToList();

                if (!linksetData.Any())
                {
                    return new ResolverResponse
                    {
                        StatusCode = 404,
                        ErrorMessage = "No data items found without qualifiers"
                    };
                }
            }

            // Replace template variables in linkset
            if (templateVariables.Any())
            {
                linksetData = await ReplaceTemplateVariablesAsync(linksetData, templateVariables);
            }

            // Generate Link header
            var linkHeader = _linksetFormatter.GenerateLinkHeader(linksetData, actualIdentifier, _fqdn);

            // Handle linkType parameter
            return await HandleLinkTypeAsync(
                linksetData,
                context,
                linkHeader,
                document,
                actualIdentifier);
        }
        catch (ValidationException vex)
        {
            return new ResolverResponse { StatusCode = 400, ErrorMessage = vex.Message };
        }
        catch (NotFoundException nfex)
        {
            return new ResolverResponse { StatusCode = 404, ErrorMessage = nfex.Message };
        }
        catch (CosmosException cosex)
        {
            return new ResolverResponse { StatusCode = 503, ErrorMessage = "Database unavailable" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error resolving {Identifier}", identifier);
            return new ResolverResponse { StatusCode = 500, ErrorMessage = "Internal server error" };
        }
    }

    private async Task<(ResolverDocument? document, string identifier, Dictionary<string, string> templateVars)> ValidateAndFetchDocumentAsync(
        string identifier,
        string? qualifierPath)
    {
        var templateVariables = new Dictionary<string, string>();
        var fullPath = identifier + (qualifierPath ?? "");

        // Validate syntax
        var syntaxResult = await _gs1Toolkit.TestDigitalLinkSyntaxAsync(fullPath);
        if (!syntaxResult)
        {
            _logger.LogWarning("Invalid Digital Link syntax: {FullPath}", fullPath);
            throw new ValidationException("Invalid GS1 Digital Link syntax"); 
        }

        // Extract AI code for serialized identifier check
        var aiCode = identifier.TrimStart('/').Split('/')[0];

        // Try to fetch document
        var docId = identifier.TrimStart('/').Replace("/", "_");
        var document = await _repository.GetByIdAsync(docId);

        // If not found and this is a serialized identifier, try partial matching
        if (document == null && SerializedAiCodes.Contains(aiCode))
        {
            var result = await ProcessSerializedIdentifierAsync(identifier);
            if (result.document != null)
            {
                document = result.document;
                identifier = result.baseIdentifier;
                templateVariables = result.templateVars;
            }
        }

        return (document, identifier, templateVariables);
    }

    private async Task<(ResolverDocument? document, string baseIdentifier, Dictionary<string, string> templateVars)> ProcessSerializedIdentifierAsync(
        string identifier)
    {
        var templateVars = new Dictionary<string, string>();
        var parts = identifier.TrimStart('/').Split('/');

        if (parts.Length < 2)
        {
            return (null, identifier, templateVars);
        }

        var aiCode = parts[0];
        var aiValue = parts[1];

        // Iteratively shorten the identifier
        for (int i = aiValue.Length - 1; i >= 0; i--)
        {
            var shortenedValue = aiValue.Substring(0, i + 1);
            var testIdentifier = $"/{aiCode}/{shortenedValue}";
            var docId = testIdentifier.TrimStart('/').Replace("/", "_");

            var document = await _repository.GetByIdAsync(docId);
            if (document != null)
            {
                // Check for template variables {0} or {1}
                var linksetJson = JsonSerializer.Serialize(document.Data);

                if (linksetJson.Contains("{0}") || linksetJson.Contains("{1}"))
                {
                    var serialComponent = aiValue.Substring(i + 1);

                    if (linksetJson.Contains("{0}"))
                    {
                        templateVars["{0}"] = serialComponent;
                    }
                    if (linksetJson.Contains("{1}"))
                    {
                        templateVars["{1}"] = serialComponent;
                    }

                    _logger.LogDebug("Found serialized identifier match with template vars: {BaseId}, serial: {Serial}",
                        testIdentifier, serialComponent);

                    return (document, testIdentifier, templateVars);
                }
            }
        }

        return (null, identifier, templateVars);
    }

    private async Task<(List<LinksetDataItem> filteredItems, Dictionary<string, string>? templateVars)> FilterItemsByQualifiersAsync(
        string qualifierPath,
        List<LinksetDataItem> linksetData)
    {
        var filteredItems = new List<LinksetDataItem>();
        var allTemplateVars = new Dictionary<string, string>();

        // Parse request qualifiers once
        var requestQualifiers = ParseQualifierPath(qualifierPath);
        if (!requestQualifiers.Any())
        {
            return (filteredItems, null);
        }

        // Iterate through each LinksetDataItem and check if its qualifiers match
        foreach (var item in linksetData)
        {
            var itemMatched = false;
            Dictionary<string, string>? itemTemplateVars = null;

            // Check if this item's qualifiers match the request
            if (item.Qualifiers != null && item.Qualifiers.Any())
            {
                // Check if ANY of the item's qualifier patterns match
                foreach (var docQualifier in item.Qualifiers)
                {
                    var (matches, vars) = CheckQualifierMatch(requestQualifiers, docQualifier);
                    if (matches)
                    {
                        itemMatched = true;
                        itemTemplateVars = vars;
                        break;
                    }
                }
            }

            // Include this item if it matched
            if (itemMatched)
            {
                filteredItems.Add(item);

                // Merge template variables from this item
                if (itemTemplateVars != null)
                {
                    foreach (var kvp in itemTemplateVars)
                    {
                        allTemplateVars[kvp.Key] = kvp.Value;
                    }
                }
            }
        }

        return (filteredItems, allTemplateVars.Any() ? allTemplateVars : null);
    }

    private async Task<(bool matches, Dictionary<string, string>? templateVars)> DoQualifiersMatchAsync(
        string qualifierPath,
        List<Dictionary<string, string>>? documentQualifiers)
    {
        if (documentQualifiers == null || !documentQualifiers.Any())
        {
            return (false, null);
        }

        // Parse qualifier path into list of dictionaries
        var requestQualifiers = ParseQualifierPath(qualifierPath);
        if (!requestQualifiers.Any())
        {
            return (false, null);
        }

        var templateVars = new Dictionary<string, string>();

        // Check if ANY document qualifier pattern matches (not ALL)
        foreach (var docQualifier in documentQualifiers)
        {
            var (matches, vars) = CheckQualifierMatch(requestQualifiers, docQualifier);
            if (matches)
            {
                return (true, vars);
            }
        }

        return (false, null);
    }

    private List<Dictionary<string, string>> ParseQualifierPath(string qualifierPath)
    {
        var qualifiers = new List<Dictionary<string, string>>();
        var parts = qualifierPath.TrimStart('/').Split('/');

        for (int i = 0; i < parts.Length - 1; i += 2)
        {
            var aiCode = parts[i];
            var aiValue = parts[i + 1];
            qualifiers.Add(new Dictionary<string, string> { { aiCode, aiValue } });
        }

        return qualifiers;
    }

    private (bool matches, Dictionary<string, string>? templateVars) CheckQualifierMatch(
        List<Dictionary<string, string>> requestQualifiers,
        Dictionary<string, string> docQualifier)
    {
        var templateVars = new Dictionary<string, string>();

        // Check if all doc qualifier keys are present in request
        foreach (var docKvp in docQualifier)
        {
            var found = false;

            foreach (var requestQualifier in requestQualifiers)
            {
                if (requestQualifier.ContainsKey(docKvp.Key))
                {
                    var requestValue = requestQualifier[docKvp.Key];
                    var docValue = docKvp.Value;

                    // Check for template variable pattern (e.g., "{cpv}")
                    var templateMatch = Regex.Match(docValue, @"\{([^}]+)\}");
                    if (templateMatch.Success)
                    {
                        var varName = "{" + templateMatch.Groups[1].Value + "}";
                        templateVars[varName] = requestValue;
                        found = true;
                        break;
                    }
                    else if (docValue == requestValue)
                    {
                        found = true;
                        break;
                    }
                }
            }

            if (!found)
            {
                return (false, null);
            }
        }

        return (true, templateVars);
    }

    private async Task<List<LinksetDataItem>> ReplaceTemplateVariablesAsync(
        List<LinksetDataItem> linksetData,
        Dictionary<string, string> templateVariables)
    {
        try
        {
            // Serialize to JSON
            var json = JsonSerializer.Serialize(linksetData);

            // Replace all template variables
            foreach (var kvp in templateVariables)
            {
                json = json.Replace(kvp.Key, kvp.Value);
            }

            // Deserialize back
            var result = JsonSerializer.Deserialize<List<LinksetDataItem>>(json);
            return result ?? linksetData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error replacing template variables");
            return linksetData;
        }
    }

    private async Task<ResolverResponse> HandleLinkTypeAsync(
        List<LinksetDataItem> linksetData,
        ResolverRequestContext context,
        string linkHeader,
        ResolverDocument document,
        string identifier)
    {
        // If linkset requested or linktype=all/linkset, return full linkset
        if (context.LinksetRequested ||
            context.Linktype?.Equals("all", StringComparison.OrdinalIgnoreCase) == true ||
            context.Linktype?.Equals("linkset", StringComparison.OrdinalIgnoreCase) == true)
        {
            var formattedLinkset = _linksetFormatter.FormatLinksetForExternalUse(
                document,
                linksetData,
                identifier,
                _fqdn);

            return new ResolverResponse
            {
                StatusCode = 200,
                Data = formattedLinkset,
                LinkHeader = linkHeader
            };
        }

        // Extract specific linktype from linkset
        var linkType = string.IsNullOrEmpty(context.Linktype) ? (document.DefaultLinktype ?? "gs1:pip"): context.Linktype; // Use document's default or fallback to pip
        var normalizedLinkType = NormalizeLinkType(linkType);
        var matchingEntries = ExtractLinkTypeEntries(linksetData, normalizedLinkType);

        // Fallback: if no matches and normalization changed the linkType,
        // try with the original linkType to support bare custom vocabularies
        if (!matchingEntries.Any() && !normalizedLinkType.Equals(linkType, StringComparison.OrdinalIgnoreCase))
        {
            matchingEntries = ExtractLinkTypeEntries(linksetData, linkType);
        }

        if (!matchingEntries.Any())
        {
            return new ResolverResponse
            {
                StatusCode = 404,
                ErrorMessage = $"No entries found for linktype: {linkType}",
                LinkHeader = linkHeader
            };
        }

        // Apply content negotiation
        var bestMatches = _contentNegotiation.GetAppropriateLinksetEntries(
            matchingEntries,
            context.AcceptLanguageList,
            context.Context,
            context.MediaTypesList,
            hasExplicitLinktype: !string.IsNullOrEmpty(context.Linktype));

        if (!bestMatches.Any())
        {
            return new ResolverResponse
            {
                StatusCode = 404,
                ErrorMessage = "No matching content found after negotiation",
                LinkHeader = linkHeader
            };
        }

        // Single match: 307 redirect
        if (bestMatches.Count == 1)
        {
            return new ResolverResponse
            {
                StatusCode = 307,
                LocationHeader = bestMatches[0].Href,
                LinkHeader = linkHeader
            };
        }

        // Multiple matches: 300 multiple choices
        var linksetArray = bestMatches.Select(e => new
        {
            href = e.Href,
            type = e.Type,
            hreflang = e.Hreflang,
            title = e.Title
        }).ToList();

        return new ResolverResponse
        {
            StatusCode = 300,
            Data = new { linkset = linksetArray },
            LinkHeader = linkHeader
        };
    }

    private List<LinksetEntry> ExtractLinkTypeEntries(List<LinksetDataItem> linksetData, string linkType)
    {
        var entries = new List<LinksetEntry>();

        foreach (var item in linksetData)
        {
            if (item.Linkset?.LinkTypes != null)
            {
                // Extract entries from all link types
                foreach (var kvp in item.Linkset.LinkTypes)
                {
                    // Handle wildcard cases
                    if (linkType == "*" || linkType == "all")
                    {
                        entries.AddRange(kvp.Value);
                        continue;
                    }

                    // Exact match (preferred for normalized IRIs)
                    if (kvp.Key.Equals(linkType, StringComparison.OrdinalIgnoreCase))
                    {
                        entries.AddRange(kvp.Value);
                        continue;
                    }

                    // Fallback: partial matching for legacy/edge cases
                    if (kvp.Key.Contains(linkType, StringComparison.OrdinalIgnoreCase) ||
                        kvp.Key.EndsWith("/" + linkType, StringComparison.OrdinalIgnoreCase))
                    {
                        entries.AddRange(kvp.Value);
                    }
                }
            }
        }

        return entries;
    }

    /// <summary>
    /// Normalizes linktype from GS1 prefix notation to full IRI.
    /// Examples: "gs1:pip" → "https://gs1.org/voc/pip"
    ///           "https://gs1.org/voc/pip" → "https://gs1.org/voc/pip" (unchanged)
    /// </summary>
    private string NormalizeLinkType(string linkType)
    {
        if (string.IsNullOrWhiteSpace(linkType))
        {
            return linkType;
        }

        // Guard: wildcards and special keywords should not be expanded
        if (linkType == "*" || linkType.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return linkType;
        }

        // Already a full URI - return as-is
        if (linkType.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            linkType.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return linkType;
        }

        // Convert gs1:xxx to full IRI
        if (linkType.StartsWith("gs1:", StringComparison.OrdinalIgnoreCase))
        {
            return GS1_VOC_BASE + linkType.Substring(4);
        }

        // Handle other prefixes if needed (schema:, etc.)
        // For now, assume bare terms should be prefixed with gs1:
        return GS1_VOC_BASE + linkType;
    }
}
