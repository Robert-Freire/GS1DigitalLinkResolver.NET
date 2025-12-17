using GS1Resolver.Shared.Models;
using Microsoft.Extensions.Logging;

namespace GS1Resolver.Shared.Services;

public class ContentNegotiationService : IContentNegotiationService
{
    private readonly ILogger<ContentNegotiationService> _logger;

    public ContentNegotiationService(ILogger<ContentNegotiationService> logger)
    {
        _logger = logger;
    }

    public List<LinksetEntry> GetAppropriateLinksetEntries(
        List<LinksetEntry> entries,
        List<string> acceptLanguages,
        string? context,
        List<string>? mediaTypes,
        bool hasExplicitLinktype = false)
    {
        if (entries == null || entries.Count == 0)
        {
            return new List<LinksetEntry>();
        }

        // Clean q-values from input lists
        var cleanedLanguages = CleanQValues(acceptLanguages);
        var cleanedMediaTypes = mediaTypes != null ? CleanQValues(mediaTypes) : null;

        // When no meaningful negotiation criteria provided AND no explicit linktype, return first entry
        if (!hasExplicitLinktype && IsDefaultNegotiation(cleanedLanguages, context, cleanedMediaTypes))
        {
            _logger.LogDebug("No negotiation criteria and no explicit linktype, returning first entry");
            return new List<LinksetEntry> { entries[0] };
        }

        // Hierarchical matching per Python logic
        var result = MatchAllThreeContexts(entries, cleanedLanguages, context, cleanedMediaTypes);
        if (result.Any()) return result;

        result = MatchLanguageAndContext(entries, cleanedLanguages, context);
        if (result.Any()) return result;

        result = MatchLanguageAndMediaType(entries, cleanedLanguages, cleanedMediaTypes);
        if (result.Any()) return result;

        result = MatchContextAndMediaType(entries, context, cleanedMediaTypes);
        if (result.Any()) return result;

        result = MatchLanguage(entries, cleanedLanguages);
        if (result.Any()) return result;

        result = MatchContext(entries, context);
        if (result.Any()) return result;

        result = MatchUndefinedLanguage(entries);
        if (result.Any()) return result;

        result = MatchMediaType(entries, cleanedMediaTypes);
        if (result.Any()) return result;

        result = MatchUndefinedMediaType(entries);
        if (result.Any()) return result;

        // Fallback: return first entry
        _logger.LogDebug("No matches found, returning first entry");
        return new List<LinksetEntry> { entries[0] };
    }

    private bool IsDefaultNegotiation(List<string> languages, string? context, List<string>? mediaTypes)
    {
        // Check if languages is ["und"] (default when no Accept-Language header)
        bool isDefaultLanguage = languages.Count == 1 &&
                                 languages[0].Equals("und", StringComparison.OrdinalIgnoreCase);

        // Check if context is null
        bool isNullContext = string.IsNullOrWhiteSpace(context);

        // Check if mediaTypes is null or contains only wildcards
        bool isWildcardMedia = mediaTypes == null ||
                               !mediaTypes.Any() ||
                               mediaTypes.All(mt => mt == "*/*" || mt == "text/*" || mt == "application/*");

        return isDefaultLanguage && isNullContext && isWildcardMedia;
    }

    private List<LinksetEntry> ResolveByLanguage(
    List<LinksetEntry> entries,
    List<string> languages)
    {
        foreach (var acceptLang in languages)
        {
            // 1. Exact matches
            var exactMatches = entries
                .Where(e => e.Hreflang != null &&
                            e.Hreflang.Any(l =>
                                l.Equals(acceptLang, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (exactMatches.Any())
            {
                return exactMatches;
            }

            // 2. Prefix matches (fallback)
            var prefixMatches = entries
                .Where(e => e.Hreflang != null &&
                            e.Hreflang.Any(l =>
                                l.StartsWith(acceptLang + "-", StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (prefixMatches.Any())
            {
                return prefixMatches;
            }
        }

        return new List<LinksetEntry>();
    }

    public List<string> CleanQValues(List<string> headerValues)
    {
        return headerValues
            .Select(v => v.Split(';')[0].Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();
    }

    private List<LinksetEntry> MatchAllThreeContexts(
        List<LinksetEntry> entries,
        List<string> languages,
        string? context,
        List<string>? mediaTypes)
    {
        if (string.IsNullOrWhiteSpace(context) || mediaTypes == null || !mediaTypes.Any())
        {
            return new List<LinksetEntry>();
        }

        return entries.Where(e =>
            LanguageMatches(e, languages) &&
            ContextMatches(e, context) &&
            MediaTypeMatches(e, mediaTypes)
        ).ToList();
    }

    private List<LinksetEntry> MatchLanguageAndContext(
        List<LinksetEntry> entries,
        List<string> languages,
        string? context)
    {
        if (string.IsNullOrWhiteSpace(context))
        {
            return new List<LinksetEntry>();
        }

        var languageFiltered = ResolveByLanguage(entries, languages);
        if (!languageFiltered.Any())
        {
            return new List<LinksetEntry>();
        }

        return languageFiltered
            .Where(e => ContextMatches(e, context))
            .ToList();
    }

    private List<LinksetEntry> MatchLanguageAndMediaType(
        List<LinksetEntry> entries,
        List<string> languages,
        List<string>? mediaTypes)
    {
        if (mediaTypes == null || !mediaTypes.Any())
        {
            return new List<LinksetEntry>();
        }

        var languageFiltered = ResolveByLanguage(entries, languages);
        if (!languageFiltered.Any())
        {
            return new List<LinksetEntry>();
        }

        return languageFiltered
            .Where(e => MediaTypeMatches(e, mediaTypes))
            .ToList();
    }

    private List<LinksetEntry> MatchContextAndMediaType(
        List<LinksetEntry> entries,
        string? context,
        List<string>? mediaTypes)
    {
        if (string.IsNullOrWhiteSpace(context) || mediaTypes == null || !mediaTypes.Any())
        {
            return new List<LinksetEntry>();
        }

        return entries.Where(e =>
            ContextMatches(e, context) &&
            MediaTypeMatches(e, mediaTypes)
        ).ToList();
    }

    private List<LinksetEntry> MatchLanguage(
    List<LinksetEntry> entries,
    List<string> languages)
    {
        return ResolveByLanguage(entries, languages);
    }

    private List<LinksetEntry> MatchContext(
        List<LinksetEntry> entries,
        string? context)
    {
        if (string.IsNullOrWhiteSpace(context))
        {
            return new List<LinksetEntry>();
        }

        return entries.Where(e => ContextMatches(e, context)).ToList();
    }

    private List<LinksetEntry> MatchMediaType(
        List<LinksetEntry> entries,
        List<string>? mediaTypes)
    {
        if (mediaTypes == null || !mediaTypes.Any())
        {
            return new List<LinksetEntry>();
        }

        return entries.Where(e => MediaTypeMatches(e, mediaTypes)).ToList();
    }

    private List<LinksetEntry> MatchUndefinedLanguage(List<LinksetEntry> entries)
    {
        return entries.Where(e =>
            e.Hreflang != null &&
            e.Hreflang.Any(lang => lang.Contains("und", StringComparison.OrdinalIgnoreCase))
        ).ToList();
    }

    private List<LinksetEntry> MatchUndefinedMediaType(List<LinksetEntry> entries)
    {
        return entries.Where(e =>
            e.Type != null &&
            e.Type.Contains("und", StringComparison.OrdinalIgnoreCase)
        ).ToList();
    }
  
    private bool LanguageMatches(LinksetEntry entry, List<string> languages)
    {
        if (entry.Hreflang == null || !entry.Hreflang.Any())
        {
            return false;
        }

        return entry.Hreflang.Any(lang =>
            languages.Any(acceptLang =>
                lang.Equals(acceptLang, StringComparison.OrdinalIgnoreCase) ||
                lang.StartsWith(acceptLang + "-", StringComparison.OrdinalIgnoreCase)
            )
        );
    }

    private bool ContextMatches(LinksetEntry entry, string context)
    {
        if (entry.Context == null || !entry.Context.Any())
        {
            return false;
        }

        return entry.Context.Any(ctx =>
            ctx.Equals(context, StringComparison.OrdinalIgnoreCase)
        );
    }

    private bool MediaTypeMatches(LinksetEntry entry, List<string> mediaTypes)
    {
        if (string.IsNullOrWhiteSpace(entry.Type))
        {
            return false;
        }

        return mediaTypes.Any(mt =>
            entry.Type.Equals(mt, StringComparison.OrdinalIgnoreCase) ||
            (mt == "*/*") ||
            (mt.EndsWith("/*") && entry.Type.StartsWith(mt.Replace("/*", ""), StringComparison.OrdinalIgnoreCase))
        );
    }
}
