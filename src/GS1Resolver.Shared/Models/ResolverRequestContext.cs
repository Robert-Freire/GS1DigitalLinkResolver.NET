namespace GS1Resolver.Shared.Models;

public record ResolverRequestContext(
    string? Linktype,
    string? Context,
    List<string> AcceptLanguageList,
    List<string>? MediaTypesList,
    bool LinksetRequested,
    bool Compress
);
