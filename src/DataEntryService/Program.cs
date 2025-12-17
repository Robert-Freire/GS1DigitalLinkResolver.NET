using DataEntryService.Middleware;
using GS1Resolver.Shared.Configuration;
using GS1Resolver.Shared.Repositories;
using GS1Resolver.Shared.Services;
using Microsoft.Azure.Cosmos;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to listen on port 3000
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(3000);
});

// Get configuration from environment variables with fallback to appsettings
var cosmosConnectionString = Environment.GetEnvironmentVariable("COSMOS_CONNECTION_STRING")
    ?? builder.Configuration["CosmosDb:ConnectionString"];

if (!string.IsNullOrEmpty(cosmosConnectionString))
{
    builder.Configuration["CosmosDb:ConnectionString"] = cosmosConnectionString;
}

var sessionToken = Environment.GetEnvironmentVariable("SESSION_TOKEN")
    ?? builder.Configuration["SessionToken:Token"];

if (!string.IsNullOrEmpty(sessionToken))
{
    builder.Configuration["SessionToken:Token"] = sessionToken;
}

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Register configuration sections
builder.Services.Configure<CosmosDbSettings>(builder.Configuration.GetSection("CosmosDb"));
builder.Services.Configure<SessionTokenSettings>(builder.Configuration.GetSection("SessionToken"));
builder.Services.Configure<GS1ToolkitSettings>(builder.Configuration.GetSection("GS1Toolkit"));

// Register Cosmos DB client as singleton
builder.Services.AddSingleton<CosmosClient>(sp =>
{
    var cosmosSettings = builder.Configuration.GetSection("CosmosDb").Get<CosmosDbSettings>();
    if (string.IsNullOrEmpty(cosmosSettings?.ConnectionString))
    {
        throw new InvalidOperationException("Cosmos DB connection string is not configured");
    }
    return new CosmosClient(cosmosSettings.ConnectionString);
});

// Register repository and services
builder.Services.AddSingleton<CosmosDbInitializer>();
builder.Services.AddScoped<IResolverRepository, CosmosDbResolverRepository>();
builder.Services.AddScoped<IProcessExecutor, ProcessExecutor>();
builder.Services.AddScoped<IGS1ToolkitService, GS1ToolkitService>();
builder.Services.AddScoped<IDataEntryLogicService, DataEntryLogicService>();

// Add controllers with JSON configuration
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.WriteIndented = true;
    });

// Configure Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure graceful shutdown
builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

// Initialize Cosmos DB and test connection on startup
try
{
    var initializer = app.Services.GetRequiredService<CosmosDbInitializer>();
    await initializer.InitializeAsync();

    var cosmosClient = app.Services.GetRequiredService<CosmosClient>();
    var cosmosSettings = builder.Configuration.GetSection("CosmosDb").Get<CosmosDbSettings>();

    if (cosmosSettings != null && !string.IsNullOrEmpty(cosmosSettings.ConnectionString))
    {
        var database = cosmosClient.GetDatabase(cosmosSettings.DatabaseName);
        var container = database.GetContainer(cosmosSettings.ContainerName);

        // Perform actual connectivity test by reading container properties
        await container.ReadContainerAsync();

        app.Logger.LogInformation("Successfully connected to Cosmos DB: {DatabaseName}/{ContainerName}",
            cosmosSettings.DatabaseName, cosmosSettings.ContainerName);
    }
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Failed to connect to Cosmos DB. Service will start but database operations may fail.");
}

// Configure middleware pipeline
app.UseCors();

// Enable Swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Data Entry API v1");
    c.RoutePrefix = "swagger";
});

// Add exception middleware BEFORE other middleware
app.UseMiddleware<ExceptionMiddleware>();

// Add authentication middleware
app.UseMiddleware<BearerTokenAuthenticationMiddleware>();

app.UseRouting();
app.UseAuthorization();
app.MapControllers();

// Register health endpoint
app.MapGet("/health", () => Results.Ok());

app.Logger.LogInformation("Data Entry Service starting on port 3000");
app.Run();

// Make the implicit Program class public for testing
namespace DataEntryService
{
    public partial class Program { }
}
