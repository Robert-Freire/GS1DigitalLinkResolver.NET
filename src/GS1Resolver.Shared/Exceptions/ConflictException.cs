namespace GS1Resolver.Shared.Exceptions;

public class ConflictException : ResolverException
{
    public ConflictException(string message) : base(409, message)
    {
    }

    public ConflictException(string message, Exception innerException)
        : base(409, message, innerException)
    {
    }
}
