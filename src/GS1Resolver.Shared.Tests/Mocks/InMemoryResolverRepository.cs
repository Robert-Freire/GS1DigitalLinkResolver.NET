using GS1Resolver.Shared.Exceptions;
using GS1Resolver.Shared.Models;
using GS1Resolver.Shared.Repositories;

namespace GS1Resolver.Shared.Tests.Mocks;

/// <summary>
/// In-memory implementation of IResolverRepository for testing when Cosmos DB emulator is unavailable.
/// Stores documents in memory using a Dictionary guarded by a lock for thread safety.
/// </summary>
public class InMemoryResolverRepository : IResolverRepository
{
    private readonly Dictionary<string, ResolverDocument> _documents = new();
    private readonly object _lock = new();

    public Task<ResolverDocument> CreateAsync(ResolverDocument document)
    {
        lock (_lock)
        {
            if (string.IsNullOrEmpty(document.Id))
            {
                throw new ValidationException("Missing 'id'");
            }

            // Use upsert behavior - create or update the document
            _documents[document.Id] = document;
            return Task.FromResult(document);
        }
    }

    public Task<ResolverDocument?> GetByIdAsync(string id)
    {
        lock (_lock)
        {
            _documents.TryGetValue(id, out var document);
            return Task.FromResult(document);
        }
    }

    public Task<ResolverDocument> UpdateAsync(string id, ResolverDocument document)
    {
        lock (_lock)
        {
            if (!_documents.ContainsKey(id))
            {
                throw new NotFoundException($"No document found with id: {id}");
            }

            document.Id = id;
            _documents[id] = document;
            return Task.FromResult(document);
        }
    }

    public Task<ResolverDocument> UpdateAsync(ResolverDocument document)
    {
        return UpdateAsync(document.Id, document);
    }

    public Task DeleteAsync(string id)
    {
        lock (_lock)
        {
            if (!_documents.ContainsKey(id))
            {
                throw new NotFoundException($"No document found with id: {id}");
            }

            _documents.Remove(id);
            return Task.CompletedTask;
        }
    }

    public Task<List<string>> GetAllDocumentIdsAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(_documents.Keys.ToList());
        }
    }

    public Task<List<ResolverDocument>> GetAllAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(_documents.Values.ToList());
        }
    }

    public Task<bool> ExistsAsync(string id)
    {
        lock (_lock)
        {
            return Task.FromResult(_documents.ContainsKey(id));
        }
    }
}
