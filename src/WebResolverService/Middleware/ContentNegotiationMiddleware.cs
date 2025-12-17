using GS1Resolver.Shared.Services;

namespace WebResolverService.Middleware;

public class ContentNegotiationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ContentNegotiationMiddleware> _logger;
    private readonly IContentNegotiationService _contentNegotiation;

    public ContentNegotiationMiddleware(
        RequestDelegate next,
        ILogger<ContentNegotiationMiddleware> logger,
        IContentNegotiationService contentNegotiation)
    {
        _next = next;
        _logger = logger;
        _contentNegotiation = contentNegotiation;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Extract and parse Accept header
        var acceptHeader = context.Request.Headers["Accept"].FirstOrDefault();
        List<string> mediaTypesList;
        if (!string.IsNullOrEmpty(acceptHeader))
        {
            var rawList = acceptHeader.Split(',')
                .Select(x => x.Trim())
                .ToList();
            mediaTypesList = _contentNegotiation.CleanQValues(rawList);
            _logger.LogDebug("Accept header: {AcceptHeader}", acceptHeader);
        }
        else
        {
            // Default to "*/*" when Accept header is missing
            mediaTypesList = new List<string> { "*/*" };
        }
        context.Items["MediaTypesList"] = mediaTypesList;

        // Extract and parse Accept-Language header
        var acceptLanguage = context.Request.Headers["Accept-Language"].FirstOrDefault();
        List<string> acceptLanguageList;
        if (!string.IsNullOrEmpty(acceptLanguage))
        {
            var rawList = acceptLanguage.Split(',')
                .Select(x => x.Trim())
                .ToList();
            acceptLanguageList = _contentNegotiation.CleanQValues(rawList);
            _logger.LogDebug("Accept-Language header: {AcceptLanguage}", acceptLanguage);
        }
        else
        {
            // Default to "und" (undefined) when Accept-Language header is missing
            acceptLanguageList = new List<string> { "und" };
        }
        context.Items["AcceptLanguageList"] = acceptLanguageList;

        // Check if linkset is requested based on cleaned media types
        var linksetRequested = mediaTypesList.Any(mt =>
            mt.Contains("application/linkset+json", StringComparison.OrdinalIgnoreCase) ||
            mt.Contains("application/json", StringComparison.OrdinalIgnoreCase));
        context.Items["LinksetRequested"] = linksetRequested;

        await _next(context);
    }
}
