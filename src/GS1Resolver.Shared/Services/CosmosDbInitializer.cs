using System.Linq;
using System.Net;
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
        var maxRetries = 5;
        var delay = TimeSpan.FromSeconds(5);

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation("Cosmos init attempt {Attempt}/{MaxRetries} - Connecting to: {ConnectionString}",
                    attempt, maxRetries, MaskConnectionString(_settings.ConnectionString));

                var dbResponse = await _client.CreateDatabaseIfNotExistsAsync(_settings.DatabaseName);
                var containerProperties = new ContainerProperties(_settings.ContainerName, _settings.PartitionKeyPath);
                await dbResponse.Database.CreateContainerIfNotExistsAsync(containerProperties);

                _logger.LogInformation("Cosmos initialized: {Database}/{Container}", _settings.DatabaseName, _settings.ContainerName);
                return;
            }
            catch (CosmosException ex)
            {
                _logger.LogWarning(ex, "CosmosException on attempt {Attempt}: StatusCode={StatusCode}, SubStatusCode={SubStatusCode}, Message={Message}",
                    attempt, ex.StatusCode, ex.SubStatusCode, ex.Message);

                if (attempt == maxRetries)
                {
                    _logger.LogError(ex, "Failed to initialize Cosmos DB after {MaxRetries} attempts", maxRetries);
                    throw;
                }

                var waitTime = delay * attempt;
                _logger.LogInformation("Retrying in {Seconds} seconds...", waitTime.TotalSeconds);
                await Task.Delay(waitTime);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unexpected exception on attempt {Attempt}: {ExceptionType} - {Message}",
                    attempt, ex.GetType().Name, ex.Message);

                if (attempt == maxRetries)
                {
                    _logger.LogError(ex, "Failed to initialize Cosmos DB after {MaxRetries} attempts", maxRetries);
                    throw;
                }

                var waitTime = delay * attempt;
                _logger.LogInformation("Retrying in {Seconds} seconds...", waitTime.TotalSeconds);
                await Task.Delay(waitTime);
            }
        }
    }

    private string MaskConnectionString(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString)) return "null";

        // Extract just the endpoint for logging (hide the account key)
        var parts = connectionString.Split(';');
        var endpoint = parts.FirstOrDefault(p => p.StartsWith("AccountEndpoint=", StringComparison.OrdinalIgnoreCase));
        return endpoint ?? "unknown";
    }
}
