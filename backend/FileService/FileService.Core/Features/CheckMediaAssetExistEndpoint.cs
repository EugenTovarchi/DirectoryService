using CSharpFunctionalExtensions;
using FileService.Contracts.Responses;
using FileService.Core.FilesStorage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SharedService.Framework.EndpointSettings;
using SharedService.SharedKernel;

namespace FileService.Core.Features;

public class CheckMediaAssetExistEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/files/{mediaAssetId:guid}/exists",
            async Task<EndpointResult<CheckMediaAssetExistResponse>> (
                [FromRoute] Guid mediaAssetId,
                [FromServices] CheckMediaAssetExistHandler handler,
                CancellationToken cancellationToken) => await handler.Handle(mediaAssetId, cancellationToken));
    }
}

public sealed class CheckMediaAssetExistHandler
{
    private readonly IFileReadDbContext _fileReadDbContext;

    public CheckMediaAssetExistHandler(
        IFileReadDbContext fileReadDbContext)
    {
        _fileReadDbContext = fileReadDbContext;
    }

    public async Task<Result<CheckMediaAssetExistResponse, Failure>> Handle(Guid mediaAssetId,
        CancellationToken cancellationToken)
    {
        bool mediaAssetIsExists =
            await _fileReadDbContext.ReadMediaAssets.AnyAsync(m => m.Id == mediaAssetId, cancellationToken);

        return new CheckMediaAssetExistResponse(mediaAssetIsExists);
    }
}