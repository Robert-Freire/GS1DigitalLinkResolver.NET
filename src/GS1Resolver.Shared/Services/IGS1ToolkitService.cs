using GS1Resolver.Shared.Models;

namespace GS1Resolver.Shared.Services;

/// <summary>
/// Service for integrating with GS1 Digital Link toolkit via Node.js subprocess calls.
/// Provides validation, compression, decompression, and analysis of GS1 Digital Links.
/// </summary>
public interface IGS1ToolkitService
{
    /// <summary>
    /// Tests Digital Link syntax by validating the URL path structure using the GS1 toolkit.
    /// </summary>
    /// <param name="urlPath">
    /// URL path portion of Digital Link (e.g., "/01/09521234543213/10/LOT/21/SERIAL").
    /// Must start with identifier AI code and follow GS1 Digital Link path format.
    /// </param>
    /// <returns>
    /// True if the URL path represents a valid GS1 Digital Link structure, false otherwise.
    /// </returns>
    Task<bool> TestDigitalLinkSyntaxAsync(string urlPath);

    /// <summary>
    /// Decompresses a compressed Digital Link and extracts structured GS1 data.
    /// </summary>
    /// <param name="compressedLink">
    /// Compressed Digital Link path (e.g., "/AQnO2IRCICDKWcnpqQs6QiOu2_A").
    /// </param>
    /// <returns>
    /// Result containing identifiers, qualifiers, data attributes, and other parameters.
    /// Returns Success=false with Error message if decompression fails.
    /// </returns>
    Task<GS1ToolkitResult> UncompressDigitalLinkAsync(string compressedLink);

    /// <summary>
    /// Compresses an uncompressed Digital Link URI into compact form.
    /// </summary>
    /// <param name="uncompressedLink">
    /// Full uncompressed Digital Link URI (e.g., "https://id.gs1.org/01/09521234543213/10/LOT").
    /// </param>
    /// <returns>
    /// Result containing compressed path in the Compressed property.
    /// Returns Success=false with Error message if compression fails.
    /// </returns>
    Task<GS1ToolkitResult> CompressDigitalLinkAsync(string uncompressedLink);

    /// <summary>
    /// Analyzes a Digital Link URI and extracts its structural components.
    /// </summary>
    /// <param name="digitalLink">
    /// Digital Link URI to analyze (compressed or uncompressed).
    /// </param>
    /// <returns>
    /// Result containing parsed identifiers, qualifiers, and data attributes.
    /// Used internally by uncompress operations.
    /// </returns>
    Task<GS1ToolkitResult> AnalyzeDigitalLinkAsync(string digitalLink);
}
