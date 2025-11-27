using System.Text.Json.Serialization;

namespace DirectoryService.SharedKernel;

public record Envelope
{
    public object? Result { get; }
    public Failure? Errors { get; }
    public DateTime TimeGenerate { get; }

    [JsonConstructor]
    private Envelope(object? result, Failure? errors)
    {
        Result = result;
        Errors = errors;
        TimeGenerate = DateTime.UtcNow;
    }

    public static Envelope Ok(object? result = null) => new(result, null);
    public static Envelope Error(Failure errors) => new(null, errors);
}

public record Envelope<T>
{
    public object? Result { get; }
    public Failure? Errors { get; }
    public DateTime TimeGenerate { get; }

    [JsonConstructor]
    private Envelope(T? result, Failure? errors)
    {
        Result = result;
        Errors = errors;
        TimeGenerate = DateTime.UtcNow;
    }

    public static Envelope<T> Ok(T? result = default) => new(result, null);
    public static Envelope<T> Error(Failure errors) => new(default, errors);
}
