using GS1Resolver.Shared.Models;

namespace GS1Resolver.Shared.Services;

public interface IContentNegotiationService
{
    /// <summary>
    /// Returns best-matching linkset entries based on hierarchical content negotiation
    /// </summary>
    List<LinksetEntry> GetAppropriateLinksetEntries(
        List<LinksetEntry> entries,
        List<string> acceptLanguages,
        string? context,
        List<string>? mediaTypes,
        bool hasExplicitLinktype = false);

    /// <summary>
    /// Cleans q-values from header values (e.g., "en;q=0.9" -> "en")
    /// </summary>
    List<string> CleanQValues(List<string> headerValues);
}
