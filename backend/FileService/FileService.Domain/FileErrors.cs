using SharedService.SharedKernel;

namespace FileService.Domain;

public static class FileErrors
{
    public static Error BucketNotFound()
    {
        return Error.NotFound("no.such.bucket", $"Bucket not found");
    }

    public static Error UploadNotFound()
    {
        return Error.NotFound("upload.id", $"Upload session not  found");
    }

    public static Error ObjectNotFound()
    {
        return Error.NotFound("object.not.found", $"Object with key not  found");
    }

    public static Error Denied()
    {
        return Error.Failure("access.denied", $"Not enough laws for this operation.");
    }

    public static Error ValidationFailed()
    {
        return Error.Failure("validation.failed", "Validation failed");
    }

    public static Error InternalServerError()
    {
        return Error.Failure("internal.server.error", $"Inner storage error.");
    }

    public static Error OperationCancelled()
    {
        return Error.Failure("operation.cancelled", $"Operation was cancelled.");
    }

    public static Error NetworkIssue()
    {
        return Error.Failure("network.issue", $"A network error occurred while communicating with file storage.");
    }

    public static Error UnknownError()
    {
        return Error.Failure("unknown.error", $"Unknown error occurred.");
    }

    public static Error HlsProcessingFailed()
    {
        return Error.Failure("hls.processing.failed", $"Video processing error occurred.");
    }

    public static Error HlsProcessingFailed(string details)
    {
        return Error.Failure("hls.processing.failed", $"Video processing has error: {details}.");
    }

    public static Error ProcessFailed()
    {
        return Error.Failure("process.failed", "Process failed");
    }

    public static Error InvalidFfprobeOutput(string details)
    {
        return Error.Failure("ffprobe.invalid.output", $"Invalid output ffprobe: {details}.");
    }
}