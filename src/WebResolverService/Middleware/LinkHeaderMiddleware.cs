using System.Text;

namespace WebResolverService.Middleware;

public class LinkHeaderMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<LinkHeaderMiddleware> _logger;

    public LinkHeaderMiddleware(
        RequestDelegate next,
        ILogger<LinkHeaderMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        // Check if controller set Link header in Items
        if (context.Items.ContainsKey("LinkHeader") && context.Items["LinkHeader"] is string linkHeader)
        {
            try
            {
                // Encode as Latin-1 with Unicode escape fallback
                var encodedHeader = EncodeLinkHeader(linkHeader);

                // Append mandatory JSON-LD context link
                var contextLink = "<http://www.w3.org/ns/json-ld#context;type=application/ld+json>; rel=\"http://www.w3.org/ns/json-ld#context\"; type=\"text/html\"";
                var combinedHeader = encodedHeader + "," + contextLink;

                // Append to existing Link header if present
                if (context.Response.Headers.ContainsKey("Link"))
                {
                    context.Response.Headers["Link"] = context.Response.Headers["Link"] + "," + combinedHeader;
                }
                else
                {
                    context.Response.Headers["Link"] = combinedHeader;
                }

                _logger.LogDebug("Added Link header to response");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error encoding Link header");
            }
        }
    }

    private string EncodeLinkHeader(string header)
    {
        try
        {
            // Try Latin-1 encoding
            var bytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(header);
            return Encoding.GetEncoding("ISO-8859-1").GetString(bytes);
        }
        catch
        {
            // Fallback to Unicode escape
            var sb = new StringBuilder();
            foreach (var c in header)
            {
                if (c > 127)
                {
                    sb.Append($"\\u{(int)c:X4}");
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
    }
}
