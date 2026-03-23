using CSharpFunctionalExtensions;
using FileService.Contracts.Requests;
using FileService.Core.FilesStorage;
using FileService.Domain;
using FileService.Domain.Assets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedService.Framework.EndpointSettings;
using SharedService.SharedKernel;

namespace FileService.Core.Features;

public sealed class GetDownloadUrlEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/files/download/url",
            async Task<EndpointResult<string>> (
                [FromBody] GetDownloadUrlRequest request,
                [FromServices] GetDownloadUrlHandler handler,
                CancellationToken cancellationToken) => await handler.Handle(request, cancellationToken));
    }
}

public sealed class GetDownloadUrlHandler
{
    private readonly ILogger<GetDownloadUrlHandler> _logger;
    private readonly IFileReadDbContext _fileReadDbContext;
    private readonly IFileStorageProvider _fileStorageProvider;

    public GetDownloadUrlHandler(
        IFileStorageProvider fileStorageProvider,
        ILogger<GetDownloadUrlHandler> logger,
        IFileReadDbContext fileReadDbContext)
    {
        _fileStorageProvider = fileStorageProvider;
        _logger = logger;
        _fileReadDbContext = fileReadDbContext;
    }

    public async Task<Result<string, Failure>> Handle(GetDownloadUrlRequest request,
        CancellationToken cancellationToken)
    {
        if (request.MediaAssetId == Guid.Empty)
            return Errors.General.ValueIsInvalid("MediaAssetId").ToFailure();

        MediaAsset? mediaAsset = await _fileReadDbContext.ReadMediaAssets
            .FirstOrDefaultAsync(m => m.Id == request.MediaAssetId
                                      && m.Status == MediaStatus.UPLOADED, cancellationToken);
        if (mediaAsset == null)
        {
            _logger.LogInformation("Media assets not found");
            return Errors.General.NotFoundEntity("MediaAssetId").ToFailure();
        }

        Result<string, Error> urlResult = await _fileStorageProvider
            .GenerateDownloadUrlAsync(mediaAsset.UploadKey, cancellationToken);
        if (urlResult.IsFailure)
        {
            _logger.LogError("Error when try to generate download url!");
            return urlResult.Error.ToFailure();
        }

        string? url = urlResult.Value;

        return url;
    }
}