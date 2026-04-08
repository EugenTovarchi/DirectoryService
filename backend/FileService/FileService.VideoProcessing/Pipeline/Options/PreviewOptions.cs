namespace FileService.VideoProcessing.Pipeline.Options;

public record PreviewOptions
{
    public const string SECTION_NAME = "PreviewOptions";

    public int Quality { get; init; } = 2;

    // Размеры кадров для превью
    public int FrameWidth { get; init; } = 320;
    public int FrameHeight { get; init; } = 180;

    // Паттерн именования файлов
    public string FileNamePattern { get; init; } = "preview_{index}.jpg";

    // Имя файла спрайт списка
    public string SpriteSheetFileName { get; init; } = "sprite_sheet.jpg";

    public int MaxPreviewCount { get; init; } = 10;

    public int MinPreviewCount { get; init; } = 3;

    public int SecondsPerPreview { get; init; } = 30;
}