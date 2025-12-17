using GS1Resolver.Shared.Models;

namespace GS1Resolver.Shared.Services;

public interface ILinksetFormatterService
{
    /// <summary>
    /// Formats linkset data for external use with JSON-LD context
    /// </summary>
    object FormatLinksetForExternalUse(
        ResolverDocument document,
        List<LinksetDataItem> matchedItems,
        string identifier,
        string fqdn);

    /// <summary>
    /// Generates Link header with pointer to linkset
    /// </summary>
    string GenerateLinkHeader(
        List<LinksetDataItem> linksetItems,
        string identifier,
        string fqdn);
}
