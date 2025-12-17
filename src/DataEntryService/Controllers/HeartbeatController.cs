using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace DataEntryService.Controllers;

/// <summary>
/// Health check endpoint for monitoring service availability.
/// </summary>
[ApiController]
[Route("api/heartbeat")]
public class HeartbeatController : ControllerBase
{
    private readonly ILogger<HeartbeatController> _logger;

    public HeartbeatController(ILogger<HeartbeatController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Check if the service is running.
    /// </summary>
    /// <returns>Service status message.</returns>
    [HttpGet]
    [SwaggerOperation(Summary = "Check service health", Description = "Returns a simple health check response")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        _logger.LogDebug("Heartbeat check");
        return Ok(new { response_message = "Server is running!" });
    }
}
