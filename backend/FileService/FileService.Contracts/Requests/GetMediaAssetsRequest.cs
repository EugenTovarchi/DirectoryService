namespace FileService.Contracts.Requests;

// Получаем набор Id из другого сервиса,
// для получения метаданных и ссылки для скачивания/ просмотра этих файлов.
public record GetMediaAssetsRequest(IReadOnlyList<Guid> MediaAssetIds);