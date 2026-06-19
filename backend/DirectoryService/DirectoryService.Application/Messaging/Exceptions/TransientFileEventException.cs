namespace DirectoryService.Application.Messaging.Exceptions;

public sealed class TransientFileEventException : Exception
{
    public TransientFileEventException(string message)
        : base(message)
    {
    }

    public TransientFileEventException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public TransientFileEventException()
    {
    }
}