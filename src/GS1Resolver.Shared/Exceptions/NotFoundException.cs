namespace GS1Resolver.Shared.Exceptions;

public class NotFoundException : ResolverException
{
    public NotFoundException(string message) : base(404, message)
    {
    }

    public NotFoundException(string message, Exception innerException)
        : base(404, message, innerException)
    {
    }
}
