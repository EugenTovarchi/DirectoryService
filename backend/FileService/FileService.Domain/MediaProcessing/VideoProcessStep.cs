using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace FileService.Domain.MediaProcessing;

public sealed class VideoProcessStep
{
    public Guid Id { get; private set; }

    public Guid VideoProcessId { get; private set; } // FK
    public int Order { get; private set; }
    public string Name { get; private set; }
    public VideoProcessStatus Status { get; private set; }
    public double Progress { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    // For EFCore.
    private VideoProcessStep() { }

    private VideoProcessStep(
        string name,
        int order,
        double progress)
    {
        Id = Guid.NewGuid();
        Name = name;
        Order = order;
        Progress = progress;
        Status = VideoProcessStatus.PENDING;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public static Result<VideoProcessStep, Error> Create(
        string name,
        int order,
        double progress)
    {
        if(string.IsNullOrWhiteSpace(name))
            return Error.Validation("name.invalid", "Name must not be empty");

        if (order <= 0)
            return Error.Validation("order.invalid", "Order must be greater than 0");

        if (progress < 0 || progress > 100)
            return Error.Validation("progress.invalid", "Progress must be between 0 and 100");

        return new VideoProcessStep(name, order, progress);
    }

    public UnitResult<Error> Start()
    {
        if (Status != VideoProcessStatus.PENDING)
        {
            return Error.Validation("step.invalid.status",
                "Step can started just from PENDING status");
        }

        Status = VideoProcessStatus.RUNNING;
        ErrorMessage = null;
        StartedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        return UnitResult.Success<Error>();
    }

    public UnitResult<Error> SetProgress(double percent)
    {
        if (Status != VideoProcessStatus.RUNNING)
            Status = VideoProcessStatus.RUNNING;

        if(percent < 0 || percent > 100)
            return Error.Validation("percent.invalid", "Percent must be between 0 and 100");

        Progress = percent;
        UpdatedAt = DateTime.UtcNow;

        return UnitResult.Success<Error>();
    }

    public UnitResult<Error> Complete()
    {
        if (Status != VideoProcessStatus.RUNNING)
        {
            return Error.Validation("step.invalid.status",
                "Step can complete just from RUNNING status");
        }

        Status = VideoProcessStatus.SUCCEEDED;
        Progress = 100;
        CompletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        return UnitResult.Success<Error>();
    }

    public UnitResult<Error> Fail(string errorMessage)
    {
        if (Status != VideoProcessStatus.RUNNING)
        {
            return Error.Validation("step.invalid.status",
                "Step can fail just from RUNNING status");
        }

        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            return Error.Validation("step.error.required",
                "Error message is required");
        }

        Status = VideoProcessStatus.FAILED;
        ErrorMessage = errorMessage;
        CompletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        return UnitResult.Success<Error>();
    }

    public void Reset()
    {
        Status = VideoProcessStatus.PENDING;
        Progress = 0;
        ErrorMessage = null;
        StartedAt = null;
        CompletedAt = null;
        UpdatedAt = DateTime.UtcNow;
    }
}