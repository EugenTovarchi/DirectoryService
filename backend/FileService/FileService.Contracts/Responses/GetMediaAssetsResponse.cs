namespace FileService.Contracts.Responses;

public record GetMediaAssetsResponse(IReadOnlyList<GetMediaAssetDto> MediaAssets);