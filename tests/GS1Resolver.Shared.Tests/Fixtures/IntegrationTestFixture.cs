using GS1Resolver.Shared.Repositories;
using GS1Resolver.Shared.Tests.Helpers;
using GS1Resolver.Shared.Tests.Mocks;
using GS1Resolver.Shared.Tests.TestData;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace GS1Resolver.Shared.Tests.Fixtures;

public class IntegrationTestFixture : IAsyncLifetime
{
    private DataEntryServiceFactory? _dataEntryFactory;
    private WebResolverServiceFactory? _webResolverFactory;
    private IResolverRepository? _sharedMockRepository;

    public HttpClient DataEntryClient { get; private set; } = null!;
    public HttpClient WebResolverClient { get; private set; } = null!;
    public List<TestDataEntry> TestDataEntries { get; private set; } = new();

    /// <summary>
    /// Indicates whether Cosmos DB emulator is available for testing.
    /// When false, tests will use in-memory mocks instead.
    /// </summary>
    public bool IsCosmosAvailable { get; private set; }

    /// <summary>
    /// Indicates whether GS1 toolkit is available for testing.
    /// When false, tests will use mock implementations instead.
    /// </summary>
    public bool IsGS1ToolkitAvailable { get; private set; }

    /// <summary>
    /// Skip message when dependencies are unavailable.
    /// Null if all dependencies are available.
    /// </summary>
    public string? SkipReason { get; private set; }

    public async Task InitializeAsync()
    {
        // Load test data files
        var testDataPath = Path.Combine(AppContext.BaseDirectory, "TestData");
        TestDataEntries = TestDataLoader.LoadTestFiles(testDataPath);

        // Load configuration
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.Test.json"), optional: true)
            .AddEnvironmentVariables()
            .Build();

        // Get configuration with environment variable overrides
        var cosmosConnectionString = DependencyDetector.GetConfigWithEnvOverride(
            configuration, "CosmosDb:ConnectionString", "TEST_COSMOS_CONNECTION_STRING");
        var nodePath = DependencyDetector.GetConfigWithEnvOverride(
            configuration, "GS1Toolkit:NodePath", "TEST_GS1_NODE_PATH") ?? "node";
        var toolkitPath = DependencyDetector.GetConfigWithEnvOverride(
            configuration, "GS1Toolkit:ToolkitPath", "TEST_GS1_TOOLKIT_PATH") ??
            "../../../data_entry_server/src/gs1-digitallink-toolkit";

        // Detect dependency availability
        IsCosmosAvailable = await DependencyDetector.IsCosmosEmulatorAvailableAsync(cosmosConnectionString ?? "");
        IsGS1ToolkitAvailable = await DependencyDetector.IsGS1ToolkitAvailableAsync(nodePath, toolkitPath);

        // Build skip reason if dependencies are missing
        var missingDependencies = new List<string>();
        if (!IsCosmosAvailable)
        {
            missingDependencies.Add("Cosmos DB emulator");
        }
        if (!IsGS1ToolkitAvailable)
        {
            SkipReason = "GS1 Digital Link toolkit not available. " +
                         "E2E tests require real toolkit for production fidelity. " +
                         "Install Node.js LTS (add to PATH) and ensure toolkit exists at " +
                         $"'{toolkitPath}' (or set TEST_GS1_TOOLKIT_PATH environment variable). " +
                         "Toolkit must contain package.json and callGS1toolkit.js.";
        }
        else if (!IsCosmosAvailable)
        {
            SkipReason = $"Dependencies unavailable: Cosmos DB emulator. " +
                        "Tests will use mock implementations instead of real dependencies.";
        }

        // If Cosmos is unavailable, create a shared in-memory repository
        // Both services will share this instance to enable cross-service data sharing
        if (!IsCosmosAvailable)
        {
            _sharedMockRepository = new InMemoryResolverRepository();
        }

        // Create factories with appropriate mocks based on dependency availability
        // Note: When Cosmos is available, we rely on database-level sharing via Cosmos DB
        // rather than sharing the IResolverRepository instance between services. This approach:
        // 1. More closely mirrors production behavior where each service has its own repository instance
        // 2. Tests the actual Cosmos DB integration and data persistence
        // 3. Validates that both services can read/write to the same database container
        // When Cosmos is unavailable, we pass a shared in-memory repository to both services.
        _dataEntryFactory = new DataEntryServiceFactory(_sharedMockRepository);
        _webResolverFactory = new WebResolverServiceFactory(_sharedMockRepository);

        // Initialize Cosmos DB if using real Cosmos (creates database/container if they don't exist)
        await _dataEntryFactory.InitializeAsync();
        await _webResolverFactory.InitializeAsync();

        // Create HTTP clients
        DataEntryClient = _dataEntryFactory.CreateClient();

        // Create WebResolver client with custom handler to prevent auto-redirect
        WebResolverClient = _webResolverFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });

        // Wait for services to be ready
        await Task.Delay(1000);
    }

    public async Task DisposeAsync()
    {
        DataEntryClient?.Dispose();
        WebResolverClient?.Dispose();

        if (_dataEntryFactory != null)
        {
            await _dataEntryFactory.DisposeAsync();
        }

        if (_webResolverFactory != null)
        {
            await _webResolverFactory.DisposeAsync();
        }
    }
}
