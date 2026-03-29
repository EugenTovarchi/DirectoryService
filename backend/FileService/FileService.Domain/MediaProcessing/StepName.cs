namespace FileService.Domain.MediaProcessing;

public class StepNames
{
    public const string Initialize = "INITIALIZE";
    public const string ExtractMetadata = "EXTRACT_METADATA";
    public const string GenerateHls = "GENERATE_HLS";
    public const string UploadHls = "UPLOAD_HLS";
    public const string GeneratePreview = "GENERATE_PREVIEW";
    public const string Cleanup = "CLEANUP";

    public readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Initialize,
        ExtractMetadata,
        GenerateHls,
        UploadHls,
        GeneratePreview,
        Cleanup
    };
}