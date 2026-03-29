using Amazon.S3.Model;
using CSharpFunctionalExtensions;
using FileService.Contracts;
using FileService.Core.Models;
using FileService.Domain;
using SharedService.SharedKernel;

namespace FileService.Core.FilesStorage;

public interface IFileStorageProvider
{
    Task<Result<string, Error>> StartMultipartUploadAsync(
        StorageKey storageKey,
        MediaData mediaData,
        CancellationToken cancellationToken = default);

    Task<Result<string, Error>> GenerateDownloadUrlAsync(StorageKey storageKey, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<MediaUrl>, Error>> GenerateDownloadUrlsAsync(
        IEnumerable<StorageKey> storageKeys, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ChunkUploadUrl>, Error>> GenerateAllChunksUploadUrlsAsync(
        StorageKey storageKey,
        string uploadId,
        int totalChunks,
        CancellationToken cancellationToken = default);

    Task<Result<string, Error>> CompleteMultipartUploadAsync(
        StorageKey storageKey,
        string uploadId,
        IReadOnlyList<PartETagDto> partETags,
        CancellationToken cancellationToken = default);

    Task<Result<string, Error>> DeleteFileAsync(StorageKey storageKey,
        CancellationToken cancellationToken = default);

    Task<UnitResult<Error>> AbortMultipartUploadAsync(
        StorageKey storageKey,
        string uploadId,
        CancellationToken cancellationToken = default);

    Task<Result<string, Error>> GenerateChunkUploadUrlAsync(
        StorageKey storageKey,
        string uploadId,
        int partNumber,
        CancellationToken cancellationToken = default);

    Task<Result<ListMultipartUploadsResponse, Error>> FileListMultipartUploadAsync(
        StorageKey storageKey, CancellationToken cancellationToken = default);

    Task<Result<bool, Error>> FileExistsAsync(StorageKey storageKey, CancellationToken cancellationToken = default);

    Task<UnitResult<Error>> UploadFileAsync(StorageKey key, Stream stream, string contentType,
        CancellationToken cancellationToken = default);
}