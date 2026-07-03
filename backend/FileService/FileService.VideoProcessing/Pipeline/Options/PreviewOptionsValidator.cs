using Microsoft.Extensions.Options;

namespace FileService.VideoProcessing.Pipeline.Options;

public sealed class PreviewOptionsValidator : IValidateOptions<PreviewOptions>
{
    public ValidateOptionsResult Validate(string? name, PreviewOptions options)
    {
        List<string> failures = [];

        if (options.Quality is < 1 or > 31)
            failures.Add("PreviewOptions:Quality must be between 1 and 31.");

        if (options.FrameWidth <= 0)
            failures.Add("PreviewOptions:FrameWidth must be greater than zero.");

        if (options.FrameHeight <= 0)
            failures.Add("PreviewOptions:FrameHeight must be greater than zero.");

        if (string.IsNullOrWhiteSpace(options.FileNamePattern))
            failures.Add("PreviewOptions:FileNamePattern is required.");

        if (string.IsNullOrWhiteSpace(options.SpriteSheetFileName))
            failures.Add("PreviewOptions:SpriteSheetFileName is required.");

        if (options.MinPreviewCount <= 0)
            failures.Add("PreviewOptions:MinPreviewCount must be greater than zero.");

        if (options.MaxPreviewCount < options.MinPreviewCount)
            failures.Add("PreviewOptions:MaxPreviewCount must not be less than MinPreviewCount.");

        if (options.SecondsPerPreview <= 0)
            failures.Add("PreviewOptions:SecondsPerPreview must be greater than zero.");

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
