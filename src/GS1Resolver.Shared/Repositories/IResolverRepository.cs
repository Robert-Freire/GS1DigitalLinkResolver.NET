using GS1Resolver.Shared.Models;

namespace GS1Resolver.Shared.Repositories;

public interface IResolverRepository
{
    Task<ResolverDocument> CreateAsync(ResolverDocument document);
    Task<ResolverDocument?> GetByIdAsync(string id);
    Task<ResolverDocument> UpdateAsync(string id, ResolverDocument document);
    Task<ResolverDocument> UpdateAsync(ResolverDocument document);
    Task DeleteAsync(string id);
    Task<List<string>> GetAllDocumentIdsAsync();
    Task<List<ResolverDocument>> GetAllAsync();
    Task<bool> ExistsAsync(string id);
}
