using GS1Resolver.Shared.Configuration;
using GS1Resolver.Shared.Exceptions;
using GS1Resolver.Shared.Models;
using GS1Resolver.Shared.Repositories;
using GS1Resolver.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace WebResolverService.Controllers;

[ApiController]
[Route("")]
public class ResolverController : ControllerBase
{
    private readonly IResolverRepository _repository;
    private readonly IGS1ToolkitService _gs1Toolkit;
    private readonly IWebResolverLogicService _resolverLogic;
    private readonly IContentNegotiationService _contentNegotiation;
    private readonly ILogger<ResolverController> _logger;
    private readonly string _fqdn;

    public ResolverController(
        IResolverRepository repository,
        IGS1ToolkitService gs1Toolkit,
        IWebResolverLogicService resolverLogic,
        IContentNegotiationService contentNegotiation,
        ILogger<ResolverController> logger,
        IOptions<FqdnSettings> fqdnSettings)
    {
        _repository = repository;
        _gs1Toolkit = gs1Toolkit;
        _resolverLogic = resolverLogic;
        _contentNegotiation = contentNegotiation;
        _logger = logger;
        _fqdn = fqdnSettings.Value.DomainName;
    }

    [HttpGet("heartbeat")]
    [HttpHead("heartbeat")]
    [HttpOptions("heartbeat")]
    public IActionResult Heartbeat()
    {
        if (Request.Method == "OPTIONS")
        {
            Response.Headers["Allow"] = "GET, HEAD, OPTIONS";
            return Ok();
        }
        return Ok(new { response_message = "Server is running!" });
    }

    [HttpGet("{aiCode}/{aiValue}", Order = 1)]
    [HttpHead("{aiCode}/{aiValue}", Order = 1)]
    [HttpOptions("{aiCode}/{aiValue}", Order = 1)]
    public async Task<IActionResult> ResolveIdentifiers(string aiCode, string aiValue)
    {
        if (Request.Method == "OPTIONS")
        {
            Response.Headers["Allow"] = "GET, HEAD, OPTIONS";
            return Ok();
        }

        _logger.LogInformation("Resolving GS1 Digital Link: /{AiCode}/{AiValue}", aiCode, aiValue);

        // Normalize GTIN-13 to GTIN-14
        if (aiCode == "01" && aiValue.Length == 13)
        {
            aiValue = "0" + aiValue;
            _logger.LogDebug("Normalized GTIN-13 to GTIN-14: {AiValue}", aiValue);
        }

        var identifier = $"/{aiCode}/{aiValue}";

        // Check for compress parameter
        var compress = Request.Query["compress"].ToString();
        if (!string.IsNullOrWhiteSpace(compress) && compress.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            var compressResult = await _gs1Toolkit.CompressDigitalLinkAsync(identifier);
            if (compressResult.Success && !string.IsNullOrWhiteSpace(compressResult.Compressed))
            {
                return Ok(new { compressedLink = compressResult.Compressed });
            }
            return StatusCode(400, new { error = $"Failed to compress Digital Link: {compressResult.Error}" });
        }

        return await ProcessResolverRequest(identifier, null);
    }

    [HttpGet("{aiCode}/{aiValue}/{*qualifiers:path}", Order = 0)]
    [HttpHead("{aiCode}/{aiValue}/{*qualifiers:path}", Order = 0)]
    [HttpOptions("{aiCode}/{aiValue}/{*qualifiers:path}", Order = 0)]
    public async Task<IActionResult> ResolveWithQualifiers(string aiCode, string aiValue, string qualifiers)
    {
        if (Request.Method == "OPTIONS")
        {
            Response.Headers["Allow"] = "GET, HEAD, OPTIONS";
            return Ok();
        }

        _logger.LogInformation("Resolving GS1 Digital Link with qualifiers: /{AiCode}/{AiValue}/{Qualifiers}",
            aiCode, aiValue, qualifiers);

        // Normalize GTIN-13 to GTIN-14
        if (aiCode == "01" && aiValue.Length == 13)
        {
            aiValue = "0" + aiValue;
            _logger.LogDebug("Normalized GTIN-13 to GTIN-14: {AiValue}", aiValue);
        }

        var identifier = $"/{aiCode}/{aiValue}";
        var qualifierPath = $"/{qualifiers}";

        // Check for compress parameter
        var compress = Request.Query["compress"].ToString();
        if (!string.IsNullOrWhiteSpace(compress) && compress.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            var pathOnly = $"{identifier}{qualifierPath}";
            var compressResult = await _gs1Toolkit.CompressDigitalLinkAsync(pathOnly);
            if (compressResult.Success && !string.IsNullOrWhiteSpace(compressResult.Compressed))
            {
                return Ok(new { compressedLink = compressResult.Compressed });
            }
            return StatusCode(400, new { error = $"Failed to compress Digital Link: {compressResult.Error}" });
        }

        return await ProcessResolverRequest(identifier, qualifierPath);
    }

    [HttpGet("{compressedOrNonGs1:regex(^(?!\\\\d{{2,4}}$).+)}", Order = 2)]
    [HttpHead("{compressedOrNonGs1:regex(^(?!\\\\d{{2,4}}$).+)}", Order = 2)]
    [HttpOptions("{compressedOrNonGs1:regex(^(?!\\\\d{{2,4}}$).+)}", Order = 2)]
    public async Task<IActionResult> ResolveCompressed(string compressedOrNonGs1)
    {
        if (Request.Method == "OPTIONS")
        {
            Response.Headers["Allow"] = "GET, HEAD, OPTIONS";
            return Ok();
        }

        // Handle .well-known/gs1resolver special case
        if (compressedOrNonGs1 == ".well-known/gs1resolver")
        {
            return await WellKnownResolver();
        }

        _logger.LogInformation("Resolving compressed/non-GS1 link: {Link}", compressedOrNonGs1);

        try
        {
            // Try to uncompress the link
            var uncompressResult = await _gs1Toolkit.UncompressDigitalLinkAsync(compressedOrNonGs1);

            if (!uncompressResult.Success)
            {
                return StatusCode(400, new { error = $"Invalid compressed Digital Link: {uncompressResult.Error}" });
            }

            // Build identifier from GS1ToolkitResult
            string identifier = "";
            string? qualifierPath = null;

            // Extract identifiers (primary keys)
            if (uncompressResult.Identifiers != null && uncompressResult.Identifiers.Any())
            {
                foreach (var idDict in uncompressResult.Identifiers)
                {
                    foreach (var kvp in idDict)
                    {
                        identifier = $"/{kvp.Key}/{kvp.Value}";
                        break; // Use first identifier as primary
                    }
                    if (!string.IsNullOrEmpty(identifier)) break;
                }
            }

            // Extract qualifiers
            if (uncompressResult.Qualifiers != null && uncompressResult.Qualifiers.Any())
            {
                var qualifierParts = new List<string>();
                foreach (var qualDict in uncompressResult.Qualifiers)
                {
                    foreach (var kvp in qualDict)
                    {
                        qualifierParts.Add(kvp.Key);
                        qualifierParts.Add(kvp.Value);
                    }
                }
                if (qualifierParts.Any())
                {
                    qualifierPath = "/" + string.Join("/", qualifierParts);
                }
            }

            if (string.IsNullOrEmpty(identifier))
            {
                return StatusCode(400, new { error = "Could not extract identifier from compressed link" });
            }

            return await ProcessResolverRequest(identifier, qualifierPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving compressed link: {Link}", compressedOrNonGs1);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet(".well-known/gs1resolver")]
    public async Task<IActionResult> WellKnownResolver()
    {
        _logger.LogDebug("Serving .well-known/gs1resolver");

        // Try to serve from public/gs1resolver.json file
        var filePath = Path.Combine(AppContext.BaseDirectory, "public", "gs1resolver.json");
        if (System.IO.File.Exists(filePath))
        {
            var json = await System.IO.File.ReadAllTextAsync(filePath);
            return Content(json, "application/json");
        }

        // Fallback to default response
        return Ok(new
        {
            resolverRoot = Request.Scheme + "://" + Request.Host.Value,
            supportedPrimaryKeys = new[] { "01", "gtin" },
            active = true
        });
    }

    private async Task<IActionResult> ProcessResolverRequest(string identifier, string? qualifierPath)
    {
        try
        {
            // Read MediaTypesList from HttpContext.Items (set by ContentNegotiationMiddleware)
            List<string> mediaTypesList;
            if (HttpContext.Items.TryGetValue("MediaTypesList", out var mediaTypesObj) && mediaTypesObj is List<string> mediaTypesFromMiddleware)
            {
                mediaTypesList = mediaTypesFromMiddleware;
            }
            else
            {
                // Fallback: parse Accept header directly
                var acceptHeader = Request.Headers["Accept"].ToString();
                if (string.IsNullOrWhiteSpace(acceptHeader))
                {
                    mediaTypesList = new List<string> { "*/*" };
                }
                else
                {
                    var rawList = acceptHeader.Split(',').Select(x => x.Trim()).ToList();
                    mediaTypesList = _contentNegotiation.CleanQValues(rawList);
                }
            }

            // Read AcceptLanguageList from HttpContext.Items (set by ContentNegotiationMiddleware)
            List<string> acceptLanguageList;
            if (HttpContext.Items.TryGetValue("AcceptLanguageList", out var languagesObj) && languagesObj is List<string> languagesFromMiddleware)
            {
                acceptLanguageList = languagesFromMiddleware;
            }
            else
            {
                // Fallback: parse Accept-Language header directly
                var acceptLanguageHeader = Request.Headers["Accept-Language"].ToString();
                if (string.IsNullOrWhiteSpace(acceptLanguageHeader))
                {
                    acceptLanguageList = new List<string> { "und" };
                }
                else
                {
                    var rawList = acceptLanguageHeader.Split(',').Select(x => x.Trim()).ToList();
                    acceptLanguageList = _contentNegotiation.CleanQValues(rawList);
                }
            }

            // Read LinksetRequested from HttpContext.Items (set by ContentNegotiationMiddleware)
            bool linksetRequested;
            if (HttpContext.Items.TryGetValue("LinksetRequested", out var linksetObj) && linksetObj is bool linksetFromMiddleware)
            {
                linksetRequested = linksetFromMiddleware;
            }
            else
            {
                // Fallback: determine if linkset is requested
                linksetRequested = mediaTypesList.Any(mt =>
                    mt.Contains("application/linkset+json", StringComparison.OrdinalIgnoreCase) ||
                    mt.Contains("application/json", StringComparison.OrdinalIgnoreCase));
            }

            // Extract query parameters
            var linktype = Request.Query["linktype"].ToString();
            var context = Request.Query["context"].ToString();

            // Override linkset request only if explicit linktype query values (all, linkset) are provided
            if (!string.IsNullOrWhiteSpace(linktype) &&
                (linktype.Equals("all", StringComparison.OrdinalIgnoreCase) ||
                 linktype.Equals("linkset", StringComparison.OrdinalIgnoreCase)))
            {
                linksetRequested = true;
            }

            // Build request context
            var requestContext = new ResolverRequestContext(
                Linktype: linktype,
                Context: context,
                AcceptLanguageList: acceptLanguageList,
                MediaTypesList: mediaTypesList,
                LinksetRequested: linksetRequested,
                Compress: false
            );

            // Call resolver logic service
            var response = await _resolverLogic.ResolveAsync(identifier, qualifierPath, requestContext);

            // Set Link header in HttpContext.Items for middleware to process
            if (!string.IsNullOrWhiteSpace(response.LinkHeader))
            {
                HttpContext.Items["LinkHeader"] = response.LinkHeader;
            }

            // Handle response based on status code
            switch (response.StatusCode)
            {
                case 307:
                    // Build Location header, excluding resolver-specific query parameters
                    var location = response.LocationHeader ?? "";

                    if (Request.QueryString.HasValue)
                    {
                        // Define resolver-specific parameters to exclude from Location header
                        var resolverParams = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        {
                            "linktype",
                            "compress",
                            "context"
                        };

                        // Filter out resolver-specific parameters
                        var cleanQueryParams = Request.Query
                            .Where(kvp => !resolverParams.Contains(kvp.Key))
                            .SelectMany(kvp => kvp.Value.Select(v => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(v)}"));

                        var cleanQuery = string.Join("&", cleanQueryParams);

                        if (!string.IsNullOrEmpty(cleanQuery))
                        {
                            location += (location.Contains("?") ? "&" : "?") + cleanQuery;
                        }
                    }

                    Response.Headers["Location"] = location;
                    return StatusCode(307);

                case 300:
                    Response.StatusCode = 300;
                    return new JsonResult(response.Data);

                case 200:
                    if (linksetRequested)
                    {
                        // Set Content-Type based on Accept header
                        var contentType = mediaTypesList.Any(mt => mt.Contains("application/linkset+json"))
                            ? "application/linkset+json"
                            : "application/json";
                        return new ContentResult
                        {
                            Content = JsonSerializer.Serialize(response.Data),
                            ContentType = contentType,
                            StatusCode = 200
                        };
                    }
                    return Ok(response.Data);

                case 400:
                case 404:
                case 500:
                    return StatusCode(response.StatusCode, new { error = response.ErrorMessage });

                default:
                    return StatusCode(500, new { error = "Unknown response status" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing resolver request for identifier: {Identifier}", identifier);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

}
