using GS1Resolver.Shared.Models;

namespace GS1Resolver.Shared.Services;

public interface IWebResolverLogicService
{
    /// <summary>
    /// Main orchestration method for resolving Digital Links
    /// </summary>
    Task<ResolverResponse> ResolveAsync(
        string identifier,
        string? qualifierPath,
        ResolverRequestContext context);
}
