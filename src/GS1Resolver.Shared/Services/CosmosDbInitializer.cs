using GS1Resolver.Shared.Configuration;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GS1Resolver.Shared.Services;

public class CosmosDbInitializer
{
    private readonly CosmosClient _client;
    private readonly CosmosDbSettings _settings;
    private readonly ILogger<CosmosDbInitializer> _logger;

    public CosmosDbInitializer(
        CosmosClient client,
        IOptions<CosmosDbSettings> settings,
        ILogger<CosmosDbInitializer> logger)
    {
        _client = client;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        var dbResponse = await _client.CreateDatabaseIfNotExistsAsync(_settings.DatabaseName);

        var containerProperties = new ContainerProperties(
            _settings.ContainerName,
            _settings.PartitionKeyPath);

        await dbResponse.Database.CreateContainerIfNotExistsAsync(containerProperties);

        _logger.LogInformation(
            "Initialized Cosmos DB: {DatabaseName}/{ContainerName}",
            _settings.DatabaseName,
            _settings.ContainerName);
    }
}
