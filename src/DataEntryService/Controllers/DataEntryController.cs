using GS1Resolver.Shared.Exceptions;
using GS1Resolver.Shared.Models;
using GS1Resolver.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace DataEntryService.Controllers;

/// <summary>
/// v3-focused Data Entry API for GS1 Digital Link resolver.
/// Handles creation, reading, updating, and deletion of linkset documents.
/// </summary>
[ApiController]
[Route("api")]
public class DataEntryController : ControllerBase
{
    private readonly IDataEntryLogicService _logicService;
    private readonly ILogger<DataEntryController> _logger;

    public DataEntryController(
        IDataEntryLogicService logicService,
        ILogger<DataEntryController> logger)
    {
        _logicService = logicService;
        _logger = logger;
    }

    /// <summary>
    /// Create or update one or more v3 documents.
    /// Accepts an array of documents.
    /// </summary>
    /// <param name="docs">List of v3 documents to create or update.</param>
    /// <returns>List of creation results with IDs and status codes.</returns>
    [HttpPost("new")]
    [SwaggerOperation(
        Summary = "Create or update documents",
        Description = "Creates new linkset documents or updates existing ones. Accepts an array of documents.")]
    [ProducesResponseType(typeof(List<CreateResult>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<CreateResult>>> CreateAsync([FromBody] List<DataEntryV3Document>? docs)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (docs == null || docs.Count == 0)
            {
                return BadRequest(new { error = "At least one document is required" });
            }

            _logger.LogInformation("POST request to create {Count} documents", docs.Count);

            var results = await _logicService.CreateDocumentAsync(docs);

            return StatusCode(StatusCodes.Status201Created, results);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error during document creation");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating documents");
            return StatusCode(500, new { error = "Internal server error", details = ex.Message });
        }
    }

    /// <summary>
    /// Create or update a single v3 document.
    /// Convenience endpoint that accepts a single document.
    /// </summary>
    /// <param name="doc">Single v3 document to create or update.</param>
    /// <returns>List of creation results with IDs and status codes.</returns>
    [HttpPost("new/single")]
    [SwaggerOperation(
        Summary = "Create or update a single document",
        Description = "Creates a new linkset document or updates an existing one. Accepts a single document.")]
    [ProducesResponseType(typeof(List<CreateResult>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<CreateResult>>> CreateSingleAsync([FromBody] DataEntryV3Document? doc)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (doc == null)
            {
                return BadRequest(new { error = "Document is required" });
            }

            _logger.LogInformation("POST request to create single document");

            var docs = new List<DataEntryV3Document> { doc };
            var results = await _logicService.CreateDocumentAsync(docs);

            return StatusCode(StatusCodes.Status201Created, results);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error during document creation");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating document");
            return StatusCode(500, new { error = "Internal server error", details = ex.Message });
        }
    }

    /// <summary>
    /// Get list of all document anchors in the system.
    /// </summary>
    /// <returns>List of formatted anchor paths (e.g., "/01/09506000134376").</returns>
    [HttpGet("index")]
    [SwaggerOperation(
        Summary = "List all document anchors",
        Description = "Returns formatted list of all GS1 Digital Link anchors in the system.")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<string>>> IndexAsync()
    {
        _logger.LogInformation("GET request for document index");
        var anchors = await _logicService.ReadIndexAsync();
        return Ok(anchors);
    }

    /// <summary>
    /// Get document by anchor AI code and value.
    /// Example: GET /api/01/09506000134376
    /// </summary>
    /// <param name="anchorAiCode">The AI code (e.g., "01" for GTIN).</param>
    /// <param name="anchorAi">The AI value.</param>
    /// <returns>List of v3 documents matching the anchor.</returns>
    [HttpGet("{anchorAiCode}/{anchorAi}")]
    [SwaggerOperation(
        Summary = "Get document by anchor",
        Description = "Retrieves v3 documents for a specific GS1 identifier.")]
    [ProducesResponseType(typeof(List<DataEntryV3Document>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<DataEntryV3Document>>> Get(string anchorAiCode, string anchorAi)
    {
        try
        {
            var id = $"{anchorAiCode}_{anchorAi}";
            _logger.LogInformation("GET request for document ID: {Id}", id);

            var docs = await _logicService.ReadDocumentAsync(id);
            return Ok(docs);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "Document not found");
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving document");
            return StatusCode(500, new { error = "Internal server error", details = ex.Message });
        }
    }

    /// <summary>
    /// Update document by anchor AI code and value.
    /// Example: PUT /api/01/09506000134376
    /// The route anchor (anchorAiCode/anchorAi) must match the anchor in each document body.
    /// If qualifiers are present in the document, the anchor should be the full path including qualifiers.
    /// </summary>
    /// <param name="anchorAiCode">The AI code (e.g., "01" for GTIN).</param>
    /// <param name="anchorAi">The AI value.</param>
    /// <param name="docs">List of v3 documents to update. Each document's Anchor must match the route anchor.</param>
    /// <returns>List of update results.</returns>
    [HttpPut("{anchorAiCode}/{anchorAi}")]
    [SwaggerOperation(
        Summary = "Update document by anchor",
        Description = "Updates existing document or creates if not exists. Route anchor must match document anchors.")]
    [ProducesResponseType(typeof(List<CreateResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(
        string anchorAiCode,
        string anchorAi,
        [FromBody] List<DataEntryV3Document> docs)
    {
        try
        {
            if (docs == null || docs.Count == 0)
            {
                return BadRequest(new { error = "Document list cannot be empty" });
            }

            _logger.LogInformation("PUT request to update {AiCode}/{Ai}", anchorAiCode, anchorAi);

            // Build the base route anchor path
            var routeAnchor = $"/{anchorAiCode}/{anchorAi}";

            // Validate that each document's anchor matches the route anchor
            for (int i = 0; i < docs.Count; i++)
            {
                var doc = docs[i];

                if (string.IsNullOrWhiteSpace(doc.Anchor))
                {
                    return BadRequest(new { error = $"Document at index {i} has missing Anchor field" });
                }

                // Normalize anchors for comparison (remove trailing slashes)
                var normalizedDocAnchor = doc.Anchor.TrimEnd('/');
                var normalizedRouteAnchor = routeAnchor.TrimEnd('/');

                // Check if document anchor starts with route anchor
                // This allows for qualifiers in the document anchor (e.g., /01/12345/21/67890)
                if (!normalizedDocAnchor.StartsWith(normalizedRouteAnchor))
                {
                    return BadRequest(new {
                        error = $"Document at index {i} has anchor '{doc.Anchor}' that does not match route anchor '{routeAnchor}'",
                        details = "The anchor path in the document must match the route parameters (anchorAiCode/anchorAi). If the document includes qualifiers, the anchor should include them as well."
                    });
                }

                // If the document anchor has additional segments beyond the route anchor,
                // verify they represent valid qualifiers
                if (normalizedDocAnchor.Length > normalizedRouteAnchor.Length)
                {
                    // Check that the next character after the route anchor is a '/'
                    if (normalizedDocAnchor[normalizedRouteAnchor.Length] != '/')
                    {
                        return BadRequest(new {
                            error = $"Document at index {i} has malformed anchor '{doc.Anchor}' that does not properly extend route anchor '{routeAnchor}'"
                        });
                    }
                }
            }

            // All validations passed - proceed with document creation
            var results = await _logicService.CreateDocumentAsync(docs);
            return Ok(results);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error during document update");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating documents");
            return StatusCode(500, new { error = "Internal server error", details = ex.Message });
        }
    }

    /// <summary>
    /// Delete document by anchor AI code and value.
    /// Example: DELETE /api/01/09506000134376
    /// </summary>
    /// <param name="anchorAiCode">The AI code (e.g., "01" for GTIN).</param>
    /// <param name="anchorAi">The AI value.</param>
    /// <returns>No content on success.</returns>
    [HttpDelete("{anchorAiCode}/{anchorAi}")]
    [SwaggerOperation(
        Summary = "Delete document by anchor",
        Description = "Deletes the document with the specified anchor.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string anchorAiCode, string anchorAi)
    {
        try
        {
            var id = $"{anchorAiCode}_{anchorAi}";
            _logger.LogInformation("DELETE request for document ID: {Id}", id);

            await _logicService.DeleteDocumentAsync(id);
            return NoContent();
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "Document not found for deletion");
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document");
            return StatusCode(500, new { error = "Internal server error", details = ex.Message });
        }
    }

    /// <summary>
    /// Get document by full path including qualifiers.
    /// Example: GET /api/01/09506000134376/21/12345
    /// </summary>
    /// <param name="anchorAiCode">The AI code (e.g., "01" for GTIN).</param>
    /// <param name="anchorAi">The AI value.</param>
    /// <param name="extraSegments">Additional path segments for qualifiers.</param>
    /// <returns>List of v3 documents matching the full path.</returns>
    [HttpGet("{anchorAiCode}/{anchorAi}/{**extraSegments}")]
    [SwaggerOperation(
        Summary = "Get document by full path with qualifiers",
        Description = "Retrieves v3 documents using complete path including qualifier segments.")]
    [ProducesResponseType(typeof(List<DataEntryV3Document>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<DataEntryV3Document>>> GetByFullPath(
        string anchorAiCode,
        string anchorAi,
        string? extraSegments = null)
    {
        try
        {
            // Build full path
            var path = $"/{anchorAiCode}/{anchorAi}";
            if (!string.IsNullOrWhiteSpace(extraSegments))
            {
                path += $"/{extraSegments}";
            }

            _logger.LogInformation("GET request for full path: {Path}", path);

            // Convert path to ID
            var id = _logicService.ConvertPathToDocumentId(path);

            var docs = await _logicService.ReadDocumentAsync(id);
            return Ok(docs);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "Document not found");
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving document by path");
            return StatusCode(500, new { error = "Internal server error", details = ex.Message });
        }
    }

    /// <summary>
    /// Migrate a legacy v2 document to v3 format.
    /// One-time migration endpoint for backwards compatibility.
    /// </summary>
    /// <param name="v2Doc">Legacy v2 document to migrate.</param>
    /// <returns>Migrated v3 documents.</returns>
    [HttpPost("migrate-v2")]
    [SwaggerOperation(
        Summary = "Migrate v2 document to v3",
        Description = "One-time migration endpoint to convert legacy v2 format to v3.")]
    [ProducesResponseType(typeof(List<DataEntryV3Document>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<DataEntryV3Document>>> MigrateV2([FromBody] DataEntryV2Document v2Doc)
    {
        try
        {
            if (v2Doc == null)
            {
                return BadRequest(new { error = "v2 document is required" });
            }

            _logger.LogInformation("POST request to migrate v2 document");

            var v3Docs = await _logicService.MigrateV2Async(v2Doc);
            return Ok(v3Docs);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error during v2 migration");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error migrating v2 document");
            return StatusCode(500, new { error = "Internal server error", details = ex.Message });
        }
    }
}
