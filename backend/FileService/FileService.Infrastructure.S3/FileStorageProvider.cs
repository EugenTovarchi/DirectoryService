using Amazon.S3;
using Amazon.S3.Model;
using CSharpFunctionalExtensions;
using FileService.Contracts;
using FileService.Core.FilesStorage;
using FileService.Core.Models;
using FileService.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedService.SharedKernel;

namespace FileService.Infrastructure.S3;

public class FileStorageProvider : IDisposable, IFileStorageProvider
{
    private readonly IAmazonS3 _s3Client;
    private readonly S3Options _s3Options;
    private readonly SemaphoreSlim _requestsSemaphore;
    private readonly ILogger<FileStorageProvider> _log;

    public FileStorageProvider(IAmazonS3 s3Client, IOptions<S3Options> s3Options, ILogger<FileStorageProvider> log)
    {
        _s3Client = s3Client;
        _log = log;
        _s3Options = s3Options.Value;
        _requestsSemaphore = new SemaphoreSlim(_s3Options.MaxConcurrentRequests);
    }

    public async Task<Result<string, Error>> StartMultipartUploadAsync(
        StorageKey storageKey,
        MediaData mediaData,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new InitiateMultipartUploadRequest
            {
                BucketName = storageKey.Location, Key = storageKey.Value, ContentType = mediaData.ContentType.Value,
            };

            var result = await _s3Client.InitiateMultipartUploadAsync(request, cancellationToken);

            return result.UploadId;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error starting multipart upload");
            return S3ErrorMapper.ToError(ex);
        }
    }

    public async Task<Result<IReadOnlyList<ChunkUploadUrl>, Error>> GenerateAllChunksUploadUrlsAsync(
        StorageKey storageKey,
        string uploadId,
        int totalChunks,
        CancellationToken cancellationToken = default)
    {
        try
        {
            IEnumerable<Task<ChunkUploadUrl>> tasks = Enumerable.Range(1, totalChunks)
                .Select(async partNumber =>
                {
                    await _requestsSemaphore.WaitAsync(cancellationToken);

                    try
                    {
                        var request = new GetPreSignedUrlRequest
                        {
                            BucketName = storageKey.Location,
                            Key = storageKey.Value,
                            Verb = HttpVerb.PUT,
                            UploadId = uploadId,
                            PartNumber = partNumber,
                            Expires = DateTime.UtcNow.AddHours(_s3Options.UploadUrlExpirationHours),
                            Protocol = _s3Options.WithSsl ? Protocol.HTTPS : Protocol.HTTP
                        };

                        string? url = await _s3Client.GetPreSignedURLAsync(request);

                        return new ChunkUploadUrl(partNumber, url);
                    }
                    finally
                    {
                        _requestsSemaphore.Release();
                    }
                });

            var results = await Task.WhenAll(tasks);

            return results;
        }
        catch (Exception ex)
        {
            return S3ErrorMapper.ToError(ex);
        }
    }

    public async Task<Result<string, Error>> GenerateChunkUploadUrlAsync(
        StorageKey storageKey,
        string uploadId,
        int partNumber,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = storageKey.Location,
                Key = storageKey.Value,
                Verb = HttpVerb.PUT,
                UploadId = uploadId,
                PartNumber = partNumber,
                Expires = DateTime.UtcNow.AddHours(_s3Options.UploadUrlExpirationHours),
                Protocol = _s3Options.WithSsl ? Protocol.HTTPS : Protocol.HTTP
            };

            string? url = await _s3Client.GetPreSignedURLAsync(request);

            return url;
        }
        catch (Exception ex)
        {
            return S3ErrorMapper.ToError(ex);
        }
    }

    public async Task<Result<IReadOnlyList<MediaUrl>, Error>> GenerateDownloadUrlsAsync(
        IEnumerable<StorageKey> storageKeys, CancellationToken cancellationToken = default)
    {
        try
        {
            IEnumerable<Task<MediaUrl>> tasks = storageKeys.Select(async storageKey =>
            {
                await _requestsSemaphore.WaitAsync(cancellationToken);

                try
                {
                    var request = new GetPreSignedUrlRequest
                    {
                        BucketName = storageKey.Location,
                        Key = storageKey.Value,
                        Verb = HttpVerb.GET,
                        Expires = DateTime.UtcNow.AddDays(_s3Options.DownloadUrlExpirationDays),
                        Protocol = _s3Options.WithSsl ? Protocol.HTTPS : Protocol.HTTP
                    };

                    string? preSignedUrl = await _s3Client.GetPreSignedURLAsync(request);

                    return new MediaUrl(storageKey, preSignedUrl);
                }
                finally
                {
                    _requestsSemaphore.Release();
                }
            });

            MediaUrl[] results = await Task.WhenAll(tasks);

            return results;
        }
        catch (Exception ex)
        {
            return S3ErrorMapper.ToError(ex);
        }
    }

    public async Task<Result<string, Error>> GenerateDownloadUrlAsync(StorageKey storageKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = storageKey.Location,
                Key = storageKey.Value,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.AddDays(_s3Options.DownloadUrlExpirationDays),
                Protocol = _s3Options.WithSsl ? Protocol.HTTPS : Protocol.HTTP
            };

            string? response = await _s3Client.GetPreSignedURLAsync(request);

            return response;
        }
        catch (Exception ex)
        {
            return S3ErrorMapper.ToError(ex);
        }
    }

    public async Task<Result<string, Error>> CompleteMultipartUploadAsync(
        StorageKey storageKey,
        string uploadId,
        IReadOnlyList<PartETagDto> partETags,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new CompleteMultipartUploadRequest
            {
                BucketName = storageKey.Location,
                Key = storageKey.Value,
                UploadId = uploadId,
                PartETags = partETags.Select(p => new PartETag { ETag = p.Etag, PartNumber = p.PartNumber }).ToList(),
            };

            CompleteMultipartUploadResponse response =
                await _s3Client.CompleteMultipartUploadAsync(request, cancellationToken);

            return response.Key;
        }
        catch (Exception ex)
        {
            return S3ErrorMapper.ToError(ex);
        }
    }

    public async Task<Result<string, Error>> DeleteFileAsync(StorageKey storageKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            DeleteObjectRequest requestForDelete =
                new DeleteObjectRequest { BucketName = storageKey.Location, Key = storageKey.Value, };

            await _s3Client.DeleteObjectAsync(requestForDelete, cancellationToken);

            _log.LogInformation("File: {key} was deleted", storageKey.Location);

            return storageKey.Key;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to delete file: {key} from bucket: {bucket}",
                storageKey.Value, storageKey.Location);
            return S3ErrorMapper.ToError(ex);
        }
    }

    public async Task<UnitResult<Error>> AbortMultipartUploadAsync(
        StorageKey storageKey,
        string uploadId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new AbortMultipartUploadRequest
            {
                BucketName = storageKey.Location, Key = storageKey.Value, UploadId = uploadId
            };

            await _s3Client.AbortMultipartUploadAsync(request, cancellationToken);

            _log.LogInformation("File: {key} was aborted to upload", storageKey.Location);

            return UnitResult.Success<Error>();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to abort multipart upload");
            return S3ErrorMapper.ToError(ex);
        }
    }

    /// <summary>
    /// Определение in-progress multipart загрузок файла или всех загрузок в bucket.
    /// </summary>
    /// <param name="storageKey">Путь к файлу/bucket в S3-хранилище.</param>
    /// <param name="cancellationToken">Стоп токен.</param>
    /// <returns> Список загрузок и их данные со статусом "UPLOADING".</returns>
    public async Task<Result<ListMultipartUploadsResponse, Error>> FileListMultipartUploadAsync(
        StorageKey storageKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new ListMultipartUploadsRequest { BucketName = storageKey.Location };

            if (!string.IsNullOrWhiteSpace(storageKey.Prefix))
            {
                request.Prefix = storageKey.Value;
            }

            ListMultipartUploadsResponse?
                result = await _s3Client.ListMultipartUploadsAsync(request, cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            return S3ErrorMapper.ToError(ex);
        }
    }

    public async Task<Result<bool, Error>> FileExistsAsync(StorageKey storageKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = storageKey.Location,
                Key = storageKey.Value
            };

            await _s3Client.GetObjectMetadataAsync(request, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
        catch (Exception ex)
        {
            return S3ErrorMapper.ToError(ex);
        }
    }

    public async Task<UnitResult<Error>> UploadFileAsync(StorageKey key, Stream stream, MediaData mediaData,
        CancellationToken cancellationToken = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = key.Location,
            ContentType = mediaData.ContentType.Value,
            Key = key.Value,
            InputStream = stream,
        };

        try
        {
            await _s3Client.PutObjectAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error uploading file");
            return S3ErrorMapper.ToError(ex);
        }

        return UnitResult.Success<Error>();
    }

    public void Dispose()
    {
        _requestsSemaphore.Release();
        _requestsSemaphore.Dispose();
    }
}