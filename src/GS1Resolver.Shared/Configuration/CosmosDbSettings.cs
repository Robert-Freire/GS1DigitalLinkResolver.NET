namespace GS1Resolver.Shared.Configuration;

public class CosmosDbSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public string PartitionKeyPath { get; set; } = "/id";
}
