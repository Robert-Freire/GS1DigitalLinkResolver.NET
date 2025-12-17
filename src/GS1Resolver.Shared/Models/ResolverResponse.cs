namespace GS1Resolver.Shared.Models;

public class ResolverResponse
{
    public int StatusCode { get; set; }
    public object? Data { get; set; }
    public string? LinkHeader { get; set; }
    public string? LocationHeader { get; set; }
    public string? ErrorMessage { get; set; }
}
