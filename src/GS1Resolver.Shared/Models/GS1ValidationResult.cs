namespace GS1Resolver.Shared.Models;

/// <summary>
/// Represents the result of GS1 AI data string validation.
/// Used when validating element strings like "(01)09521234543213(10)LOT(21)SERIAL".
/// </summary>
public class GS1ValidationResult
{
    /// <summary>
    /// Indicates whether the AI data string is valid according to GS1 syntax rules.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// The generated Digital Link URI from the AI data string.
    /// Example: "https://id.gs1.org/01/09521234543213/10/LOT/21/SERIAL"
    /// Only populated when IsValid is true.
    /// </summary>
    public string? DigitalLinkUri { get; set; }

    /// <summary>
    /// Error message describing why validation failed.
    /// Only populated when IsValid is false.
    /// </summary>
    public string? Error { get; set; }
}
