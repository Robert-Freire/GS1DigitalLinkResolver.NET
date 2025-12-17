using GS1Resolver.Shared.Configuration;
using GS1Resolver.Shared.Exceptions;
using GS1Resolver.Shared.Models;
using GS1Resolver.Shared.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GS1Resolver.Shared.Repositories;

public class CosmosDbResolverRepository : IResolverRepository
{
    private readonly Container _container;
    private readonly ILogger<CosmosDbResolverRepository> _logger;
    private readonly CosmosDbInitializer _initializer;

    public CosmosDbResolverRepository(
        CosmosClient cosmosClient,
        IOptions<CosmosDbSettings> settings,
        ILogger<CosmosDbResolverRepository> logger,
        CosmosDbInitializer initializer)
    {
        _logger = logger;
        _initializer = initializer;
        var cosmosSettings = settings.Value;

        var database = cosmosClient.GetDatabase(cosmosSettings.DatabaseName);
        _container = database.GetContainer(cosmosSettings.ContainerName);

        _logger.LogInformation("CosmosDbResolverRepository initialized with database: {DatabaseName}, container: {ContainerName}",
            cosmosSettings.DatabaseName, cosmosSettings.ContainerName);
    }

    public async Task<ResolverDocument?> GetByIdAsync(string id)
    {
        try
        {
            var response = await _container.ReadItemAsync<ResolverDocument>(
                id,
                new PartitionKey(id));

            _logger.LogDebug("Retrieved document with ID: {Id}", id);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogDebug("Document with ID {Id} not found", id);
            return null;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Cosmos error retrieving document with ID: {Id}", id);
            throw new ResolverException((int)ex.StatusCode, $"Database error: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving document with ID: {Id}", id);
            throw;
        }
    }

    public async Task<ResolverDocument> CreateAsync(ResolverDocument document)
    {
        try
        {
            if (string.IsNullOrEmpty(document.Id))
            {
                throw new ValidationException("Missing 'id'");
            }

            await _initializer.InitializeAsync();

            var response = await _container.UpsertItemAsync(
                document,
                new PartitionKey(document.Id));

            _logger.LogInformation("Upserted document with ID: {Id}", document.Id);
            return response.Resource;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Cosmos error upserting document with ID: {Id}", document.Id);
            throw new ResolverException((int)ex.StatusCode, $"Database error: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is not ResolverException)
        {
            _logger.LogError(ex, "Error upserting document with ID: {Id}", document.Id);
            throw;
        }
    }

    public async Task<ResolverDocument> UpdateAsync(string id, ResolverDocument document)
    {
        try
        {
            document.Id = id;
            var response = await _container.ReplaceItemAsync(
                document,
                id,
                new PartitionKey(id));

            _logger.LogInformation("Updated document with ID: {Id}", id);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogDebug("Document with ID {Id} not found for update", id);
            throw new NotFoundException($"No document found with id: {id}", ex);
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Cosmos error updating document with ID: {Id}", id);
            throw new ResolverException((int)ex.StatusCode, $"Database error: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is not ResolverException)
        {
            _logger.LogError(ex, "Error updating document with ID: {Id}", id);
            throw;
        }
    }

    public async Task<ResolverDocument> UpdateAsync(ResolverDocument document)
    {
        return await UpdateAsync(document.Id, document);
    }

    public async Task DeleteAsync(string id)
    {
        try
        {
            await _container.DeleteItemAsync<ResolverDocument>(
                id,
                new PartitionKey(id));

            _logger.LogInformation("Deleted document with ID: {Id}", id);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogDebug("Document with ID {Id} not found for deletion", id);
            throw new NotFoundException($"No document found with id: {id}", ex);
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Cosmos error deleting document with ID: {Id}", id);
            throw new ResolverException((int)ex.StatusCode, $"Database error: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is not ResolverException)
        {
            _logger.LogError(ex, "Error deleting document with ID: {Id}", id);
            throw;
        }
    }

    public async Task<bool> ExistsAsync(string id)
    {
        try
        {
            await _container.ReadItemAsync<ResolverDocument>(
                id,
                new PartitionKey(id));

            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Cosmos error checking existence of document with ID: {Id}", id);
            throw new ResolverException((int)ex.StatusCode, $"Database error: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking existence of document with ID: {Id}", id);
            throw;
        }
    }

    public async Task<List<string>> GetAllDocumentIdsAsync()
    {
        try
        {
            var queryDefinition = new QueryDefinition("SELECT c.id FROM c");
            var queryIterator = _container.GetItemQueryIterator<dynamic>(queryDefinition);

            var documentIds = new List<string>();
            while (queryIterator.HasMoreResults)
            {
                var response = await queryIterator.ReadNextAsync();
                foreach (var item in response)
                {
                    if (item.id != null)
                    {
                        documentIds.Add(item.id.ToString());
                    }
                }
            }

            _logger.LogDebug("Retrieved {Count} document IDs", documentIds.Count);
            return documentIds;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Cosmos error retrieving document IDs");
            throw new ResolverException((int)ex.StatusCode, $"Database error: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving document IDs");
            throw;
        }
    }

    public async Task<List<ResolverDocument>> GetAllAsync()
    {
        try
        {
            var queryDefinition = new QueryDefinition("SELECT * FROM c");
            var queryIterator = _container.GetItemQueryIterator<ResolverDocument>(queryDefinition);

            var documents = new List<ResolverDocument>();
            while (queryIterator.HasMoreResults)
            {
                var response = await queryIterator.ReadNextAsync();
                documents.AddRange(response);
            }

            _logger.LogDebug("Retrieved {Count} documents", documents.Count);
            return documents;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Cosmos error retrieving all documents");
            throw new ResolverException((int)ex.StatusCode, $"Database error: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all documents");
            throw;
        }
    }
}
