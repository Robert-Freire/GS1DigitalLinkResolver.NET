using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using GS1Resolver.Shared.Repositories;
using GS1Resolver.Shared.Services;
using GS1Resolver.Shared.Tests.Mocks;
using Microsoft.Azure.Cosmos;
using GS1Resolver.Shared.Configuration;
using System.Net.Http;

namespace GS1Resolver.Shared.Tests.Fixtures;

public class DataEntryServiceFactory : WebApplicationFactory<DataEntryService.Program>
{
    private readonly IResolverRepository? _sharedRepository;

    public DataEntryServiceFactory(IResolverRepository? sharedRepository = null)
    {
        _sharedRepository = sharedRepository;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Add test-specific configuration from test output folder
            config.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.Test.json"), optional: true);

            // Override with test values including valid Cosmos DB connection string
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CosmosDb:ConnectionString"] = "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
                ["CosmosDb:DatabaseName"] = "GS1ResolverTest_v4",
                ["CosmosDb:ContainerName"] = "resolver_test_v4",
                ["SessionToken:Token"] = "secret",
                ["Fqdn:DomainName"] = "localhost:8080"
            });
        });

        builder.ConfigureServices(services =>
        {
            // If a shared repository is provided, replace the registered one
            if (_sharedRepository != null)
            {
                // Remove the existing IResolverRepository registration
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IResolverRepository));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Add the shared repository instance
                services.AddSingleton(_sharedRepository);
            }
            else
            {
                // If using real Cosmos DB, replace the CosmosClient with one configured for emulator
                var cosmosClientDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(CosmosClient));
                if (cosmosClientDescriptor != null)
                {
                    services.Remove(cosmosClientDescriptor);
                }

                // Add CosmosClient configured for emulator with SSL validation disabled
                services.AddSingleton<CosmosClient>(sp =>
                {
                    var configuration = sp.GetRequiredService<IConfiguration>();
                    var cosmosSettings = configuration.GetSection("CosmosDb").Get<CosmosDbSettings>();
                    if (string.IsNullOrEmpty(cosmosSettings?.ConnectionString))
                    {
                        throw new InvalidOperationException("Cosmos DB connection string is not configured");
                    }

                    var clientOptions = new CosmosClientOptions
                    {
                        HttpClientFactory = () =>
                        {
                            var httpMessageHandler = new HttpClientHandler
                            {
                                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
                                ClientCertificateOptions = ClientCertificateOption.Manual,
                                SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13
                            };
                            return new HttpClient(httpMessageHandler);
                        },
                        ConnectionMode = ConnectionMode.Gateway,
                        RequestTimeout = TimeSpan.FromSeconds(30)
                    };

                    return new CosmosClient(cosmosSettings.ConnectionString, clientOptions);
                });
            }
        });

        builder.UseEnvironment("Test");
    }

    public async Task InitializeAsync()
    {
        // Only initialize Cosmos DB if we're not using a shared in-memory repository
        if (_sharedRepository == null)
        {
            try
            {
                var initializer = Services.GetRequiredService<CosmosDbInitializer>();
                await initializer.InitializeAsync();
            }
            catch (Exception)
            {
                // Initialization errors are logged by CosmosDbInitializer
                // If Cosmos is unavailable, tests should handle it gracefully
            }
        }
    }
}
