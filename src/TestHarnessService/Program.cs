using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to listen on port 5000
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000);
});

// Add controllers
builder.Services.AddControllers();

// Configure HttpClient for test requests with HTTP/2 support
builder.Services.AddHttpClient("TestClient", client =>
{
    client.DefaultRequestVersion = new Version(2, 0);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    // For testing only - accept any SSL certificate
    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
    // Enable HTTP/2
    SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13,
    // Disable automatic redirects to match Python test harness behavior
    AllowAutoRedirect = false
});

// Add directory browsing (disabled by default)
builder.Services.AddDirectoryBrowser();

var app = builder.Build();

// Configure static file serving
var staticFilesPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
if (!Directory.Exists(staticFilesPath))
{
    Directory.CreateDirectory(staticFilesPath);
    app.Logger.LogWarning("wwwroot directory did not exist, created: {Path}", staticFilesPath);
}

// IMPORTANT: UseDefaultFiles must come BEFORE UseStaticFiles
app.UseDefaultFiles(new DefaultFilesOptions
{
    FileProvider = new PhysicalFileProvider(staticFilesPath),
    RequestPath = ""
});

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(staticFilesPath),
    RequestPath = ""
});

app.UseRouting();
app.MapControllers();

app.Logger.LogInformation("Test Harness Service starting on port 5000");
app.Logger.LogInformation("Serving static files from: {Path}", staticFilesPath);
app.Run();
