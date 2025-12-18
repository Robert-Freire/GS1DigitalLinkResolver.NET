using GS1Resolver.Shared.Configuration;
using GS1Resolver.Shared.Repositories;
using GS1Resolver.Shared.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Azure.Cosmos;
using WebResolverService.Constraints;
using WebResolverService.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to listen on port 4000
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(4000);
});

// Get configuration from environment variables with fallback to appsettings
var cosmosConnectionString = Environment.GetEnvironmentVariable("COSMOS_CONNECTION_STRING")
    ?? builder.Configuration["CosmosDb:ConnectionString"];

if (!string.IsNullOrEmpty(cosmosConnectionString))
{
    builder.Configuration["CosmosDb:ConnectionString"] = cosmosConnectionString;
}

var fqdn = Environment.GetEnvironmentVariable("FQDN")
    ?? builder.Configuration["Fqdn:DomainName"];

if (!string.IsNullOrEmpty(fqdn))
{
    builder.Configuration["Fqdn:DomainName"] = fqdn;
}

// Configure CORS for GS1 Digital Link resolution
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .WithMethods("GET", "HEAD", "OPTIONS")
              .AllowAnyHeader()
              .WithExposedHeaders("Link", "Content-Type");
    });
});

// Register configuration sections
builder.Services.Configure<CosmosDbSettings>(builder.Configuration.GetSection("CosmosDb"));
builder.Services.Configure<FqdnSettings>(builder.Configuration.GetSection("Fqdn"));
builder.Services.Configure<GS1ToolkitSettings>(builder.Configuration.GetSection("GS1Toolkit"));

// Register Cosmos DB client as singleton
builder.Services.AddSingleton<CosmosClient>(sp =>
{
    var cosmosSettings = builder.Configuration.GetSection("CosmosDb").Get<CosmosDbSettings>();
    if (string.IsNullOrEmpty(cosmosSettings?.ConnectionString))
    {
        throw new InvalidOperationException("Cosmos DB connection string is not configured");
    }

    var clientOptions = new CosmosClientOptions
    {
        ConnectionMode = ConnectionMode.Gateway,
        RequestTimeout = TimeSpan.FromSeconds(30),
        HttpClientFactory = () =>
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true,
                ClientCertificateOptions = ClientCertificateOption.Manual,
                SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13
            };
            return new HttpClient(handler);
        }
    };

    return new CosmosClient(cosmosSettings.ConnectionString, clientOptions);
});

// Register repository and services
builder.Services.AddSingleton<CosmosDbInitializer>();
builder.Services.AddScoped<IResolverRepository, CosmosDbResolverRepository>();
builder.Services.AddScoped<IProcessExecutor, ProcessExecutor>();
builder.Services.AddScoped<IGS1ToolkitService, GS1ToolkitService>();

// Register resolver logic services
builder.Services.AddScoped<IWebResolverLogicService, WebResolverLogicService>();
builder.Services.AddScoped<IContentNegotiationService, ContentNegotiationService>();
builder.Services.AddScoped<ILinksetFormatterService, LinksetFormatterService>();

// Add controllers
builder.Services.AddControllers();

// Configure Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure route options
builder.Services.Configure<RouteOptions>(options =>
{
    options.LowercaseUrls = true;
    options.ConstraintMap["path"] = typeof(PathRouteConstraint);
});

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
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Web Resolver API v1");
    c.RoutePrefix = "swagger";
});

// Add exception middleware BEFORE other middleware
app.UseMiddleware<ExceptionMiddleware>();

// Add custom middleware
app.UseMiddleware<ContentNegotiationMiddleware>();
app.UseMiddleware<LinkHeaderMiddleware>();

app.UseRouting();
app.UseAuthorization();
app.MapControllers();

app.Logger.LogInformation("Web Resolver Service starting on port 4000");
app.Logger.LogInformation("FQDN configured as: {FQDN}", fqdn ?? "not set");
app.Run();

// Make the implicit Program class public for testing
namespace WebResolverService
{
    public partial class Program { }
}
