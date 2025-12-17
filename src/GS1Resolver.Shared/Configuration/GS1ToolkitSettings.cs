namespace GS1Resolver.Shared.Configuration;

/// <summary>
/// Configuration settings for GS1 Digital Link toolkit integration.
/// Defines paths to Node.js executable and GS1 toolkit scripts used for validation, compression, and analysis.
/// </summary>
public class GS1ToolkitSettings
{
    /// <summary>
    /// Path to Node.js executable.
    /// Default: "node" (uses PATH lookup).
    /// Docker: "/usr/bin/node" for absolute path.
    /// </summary>
    public string NodePath { get; set; } = "node";

    /// <summary>
    /// Path to GS1 Digital Link toolkit directory containing Node.js scripts and npm dependencies.
    /// Development: Relative path like "../../data_entry_server/src/gs1-digitallink-toolkit".
    /// Docker: "/app/gs1-digitallink-toolkit".
    /// </summary>
    public string ToolkitPath { get; set; } = "/app/gs1-digitallink-toolkit";

    /// <summary>
    /// Script name for GS1 toolkit operations (validation, compression, decompression, analysis).
    /// Default: "callGS1toolkit.js"
    /// </summary>
    public string ToolkitScriptName { get; set; } = "callGS1toolkit.js";

    /// <summary>
    /// Gets the full path to the toolkit script.
    /// </summary>
    public string ToolkitScriptPath => Path.Combine(ToolkitPath, ToolkitScriptName);
}
