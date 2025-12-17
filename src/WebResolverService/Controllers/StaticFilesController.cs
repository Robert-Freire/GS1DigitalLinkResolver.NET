using Microsoft.AspNetCore.Mvc;

namespace WebResolverService.Controllers;

[ApiController]
[Route("")]
public class StaticFilesController : ControllerBase
{
    private readonly ILogger<StaticFilesController> _logger;
    private readonly IWebHostEnvironment _environment;

    public StaticFilesController(
        ILogger<StaticFilesController> logger,
        IWebHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    [HttpGet("robots.txt")]
    public IActionResult RobotsTxt()
    {
        _logger.LogDebug("Serving robots.txt");
        var filePath = Path.Combine(_environment.ContentRootPath, "public", "robots.txt");

        if (!System.IO.File.Exists(filePath))
        {
            return NotFound();
        }

        var content = System.IO.File.ReadAllText(filePath);
        return Content(content, "text/plain");
    }

    [HttpGet("favicon.ico")]
    public IActionResult Favicon()
    {
        _logger.LogDebug("Serving favicon.ico");
        var filePath = Path.Combine(_environment.ContentRootPath, "public", "favicon.ico");

        if (!System.IO.File.Exists(filePath))
        {
            return NotFound();
        }

        var bytes = System.IO.File.ReadAllBytes(filePath);
        return File(bytes, "image/x-icon");
    }

    [HttpGet("gs1resolver.json")]
    public IActionResult Gs1ResolverJson()
    {
        _logger.LogDebug("Serving gs1resolver.json");
        var filePath = Path.Combine(_environment.ContentRootPath, "public", "gs1resolver.json");

        if (!System.IO.File.Exists(filePath))
        {
            return NotFound();
        }

        var content = System.IO.File.ReadAllText(filePath);
        return Content(content, "application/json");
    }
}
