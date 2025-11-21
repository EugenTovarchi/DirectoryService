namespace DirectoryService.SharedKernel;

public record Envelope
{
    public object? Result { get; }
    public Failure? Errors { get; }
    public DateTime TimeGenerate { get; }

    private Envelope(object? result, Failure? errors)
    {
        Result = result;
        Errors = errors;
        TimeGenerate = DateTime.Now;
    }

    public static Envelope Ok(object? result = null) => new(result, null);
    public static Envelope Error(Failure errors) => new(null, errors);
}
