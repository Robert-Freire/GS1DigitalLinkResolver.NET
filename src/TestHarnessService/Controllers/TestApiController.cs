using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;

namespace TestHarnessService.Controllers;

[ApiController]
[Route("api")]
public class TestApiController : ControllerBase
{
    private readonly ILogger<TestApiController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public TestApiController(
        ILogger<TestApiController> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? test, [FromQuery] string? testVal)
    {
        if (string.IsNullOrEmpty(test))
        {
            return Ok(new { error = "No command received" });
        }

        _logger.LogDebug("Test API called with test: {Test}, testVal: {TestVal}", test, testVal);

        var httpClient = _httpClientFactory.CreateClient("TestClient");

        switch (test)
        {
            case "getHTTPversion":
                {
                    if (string.IsNullOrEmpty(testVal))
                    {
                        return Ok(new
                        {
                            test = "getHTTPversion",
                            testVal = testVal ?? string.Empty,
                            result = new { error = "testVal is required for getHTTPversion" }
                        });
                    }

                    try
                    {
                        // Perform HEAD request to the domain
                        var requestUri = testVal.StartsWith("http") ? testVal : $"https://{testVal}";
                        var headRequest = new HttpRequestMessage(HttpMethod.Head, requestUri);
                        var response = await httpClient.SendAsync(headRequest);

                        var httpVersion = response.Version.ToString();
                        // Format as HTTP/1.1 or HTTP/2.0
                        if (response.Version.Major == 2)
                        {
                            httpVersion = "HTTP/2";
                        }
                        else
                        {
                            httpVersion = $"HTTP/{response.Version}";
                        }

                        _logger.LogDebug("HTTP version for {TestVal}: {HttpVersion}", testVal, httpVersion);

                        return Ok(new
                        {
                            test = "getHTTPversion",
                            testVal = testVal,
                            result = httpVersion
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error executing getHTTPversion with testVal {TestVal}", testVal);
                        return Ok(new
                        {
                            test = "getHTTPversion",
                            testVal = testVal,
                            result = new { error = ex.Message }
                        });
                    }
                }

            case "getAllHeaders":
            {
                if (string.IsNullOrEmpty(testVal))
                {
                    return Ok(new
                    {
                        test = "getAllHeaders",
                        testVal = testVal ?? string.Empty,
                        result = new { error = "testVal is required for getAllHeaders" }
                    });
                }

                try
                {
                    // Perform HEAD request with no redirects
                    var headRequest = new HttpRequestMessage(HttpMethod.Head, testVal);
                    var response = await httpClient.SendAsync(headRequest, HttpCompletionOption.ResponseHeadersRead);

                    // Build result object
                    var queryString = Request.QueryString.Value ?? string.Empty;
                    var requestURI = queryString.StartsWith("?") ? queryString.Substring(1) : queryString;

                    var resultObject = new Dictionary<string, object>
                    {
                        ["httpCode"] = (int)response.StatusCode,
                        ["httpMsg"] = response.ReasonPhrase ?? string.Empty,
                        ["requestURI"] = requestURI,
                        ["Requesting_User_Agent"] = Request.Headers["User-Agent"].ToString(),
                        ["Requesting_Accept_Header"] = Request.Headers["Accept"].ToString(),
                        ["Requesting_Accept_Language"] = Request.Headers["Accept-Language"].ToString()
                    };

                    // Add all response headers to the result object
                    foreach (var header in response.Headers)
                    {
                        resultObject[header.Key] = string.Join(", ", header.Value);
                    }

                    foreach (var header in response.Content.Headers)
                    {
                        resultObject[header.Key] = string.Join(", ", header.Value);
                    }

                    _logger.LogDebug("getAllHeaders for {TestVal}: Status {StatusCode}", testVal, response.StatusCode);

                    return Ok(new
                    {
                        test = "getAllHeaders",
                        testVal = testVal,
                        result = resultObject
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing getAllHeaders with testVal {TestVal}", testVal);
                    return Ok(new
                    {
                        test = "getAllHeaders",
                        testVal = testVal,
                        result = new { error = ex.Message }
                    });
                }
            }

            default:
                return Ok(new { error = $"Unknown test command: {test}" });
        }
    }
}
