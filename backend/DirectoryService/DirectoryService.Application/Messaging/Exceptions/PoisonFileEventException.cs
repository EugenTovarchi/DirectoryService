namespace DirectoryService.Application.Messaging.Exceptions;

public sealed class PoisonFileEventException : ArgumentException
{
    public PoisonFileEventException(string message)
        : base(message)
    {
    }

    public PoisonFileEventException()
    {
    }

    public PoisonFileEventException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}