namespace GS1Resolver.Shared.Exceptions;

public class ValidationException : ResolverException
{
    public ValidationException(string message) : base(400, message)
    {
    }

    public ValidationException(string message, Exception innerException)
        : base(400, message, innerException)
    {
    }
}
