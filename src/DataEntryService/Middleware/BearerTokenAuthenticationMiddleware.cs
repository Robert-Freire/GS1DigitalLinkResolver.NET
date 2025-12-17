using GS1Resolver.Shared.Configuration;
using Microsoft.Extensions.Options;

namespace DataEntryService.Middleware;

public class BearerTokenAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<BearerTokenAuthenticationMiddleware> _logger;

    public BearerTokenAuthenticationMiddleware(
        RequestDelegate next,
        ILogger<BearerTokenAuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IOptions<SessionTokenSettings> tokenSettings)
    {
        // Skip authentication for health check, Swagger, heartbeat, index, and migration endpoints
        if (context.Request.Path.StartsWithSegments("/health") ||
            context.Request.Path.StartsWithSegments("/swagger") ||
            context.Request.Path.StartsWithSegments("/api/heartbeat") ||
            context.Request.Path.StartsWithSegments("/api/index") ||
            context.Request.Path.StartsWithSegments("/api/migrate-v2"))
        {
            await _next(context);
            return;
        }

        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();

        if (string.IsNullOrEmpty(authHeader))
        {
            _logger.LogWarning("Missing Authorization header");
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Unauthorized: Missing Authorization header");
            return;
        }

        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Invalid Authorization header format");
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Unauthorized: Invalid Authorization header format");
            return;
        }

        var token = authHeader.Substring("Bearer ".Length).Trim();
        var expectedToken = tokenSettings.Value.Token;

        if (token != expectedToken)
        {
            _logger.LogWarning("Invalid bearer token");
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Unauthorized: Invalid token");
            return;
        }

        await _next(context);
    }
}
