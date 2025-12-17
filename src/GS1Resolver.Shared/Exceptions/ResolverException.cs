namespace GS1Resolver.Shared.Exceptions;

public class ResolverException : Exception
{
    public int StatusCode { get; }

    public ResolverException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }

    public ResolverException(int statusCode, string message, Exception innerException)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}
