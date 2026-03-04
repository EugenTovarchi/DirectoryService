using Amazon.S3;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace FileService.Core.Features;

[ApiController]
[Route("api/[controller]")]
public class S3TestController : ControllerBase
{
    private readonly IAmazonS3 _s3Client;
    private readonly ILogger<S3TestController> _logger;

    public S3TestController(IAmazonS3 s3Client, ILogger<S3TestController> logger)
    {
        _s3Client = s3Client;
        _logger = logger;
    }

    [HttpGet("test-connection")]
    public async Task<IActionResult> TestConnection()
    {
        try
        {
            _logger.LogInformation("Testing S3 connection to {ServiceUrl}", _s3Client.Config.ServiceURL);

            // Пробуем получить список бакетов
            var response = await _s3Client.ListBucketsAsync();

            return Ok(new
            {
                Success = true,
                ServiceUrl = _s3Client.Config.ServiceURL,
                _s3Client.Config.UseHttp,
                Buckets = response.Buckets.Select(b => b.BucketName).ToList(),
                Message = "Successfully connected to MinIO"
            });
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 connection test failed with AmazonS3Exception");
            return StatusCode(500,
                new
                {
                    Success = false,
                    ServiceUrl = _s3Client.Config.ServiceURL,
                    ex.ErrorCode,
                    StatusCode = ex.StatusCode.ToString(),
                    ex.Message,
                    IsBadGateway = ex.StatusCode == System.Net.HttpStatusCode.BadGateway
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "S3 connection test failed");
            return StatusCode(500,
                new
                {
                    Success = false,
                    ServiceUrl = _s3Client.Config.ServiceURL,
                    Error = ex.Message,
                    InnerError = ex.InnerException?.Message
                });
        }
    }

    [HttpGet("test-bucket/{bucketName}/or-create")]
    public async Task<IActionResult> TestBucket(string bucketName)
    {
        try
        {
            // Проверяем существование бакета
            bool exists = await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, bucketName);

            if (exists)
            {
                return Ok(new { Message = $"Bucket {bucketName} exists" });
            }

            // Пробуем создать бакет
            await _s3Client.PutBucketAsync(bucketName);
            return Ok(new { Message = $"Bucket {bucketName} created successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("list-buckets")]
    public async Task<IActionResult> ListBuckets()
    {
        try
        {
            var response = await _s3Client.ListBucketsAsync();
            return Ok(new { Success = true, Buckets = response.Buckets.Select(b => b.BucketName).ToList() });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("check-bucket/{bucketName}/just-get")]
    public async Task<IActionResult> CheckBucketDetailed(string bucketName)
    {
        try
        {
            // Способ 1: ListBuckets (надежный)
            var allBuckets = await _s3Client.ListBucketsAsync();
            bool existsInList = allBuckets.Buckets.Any(b => b.BucketName == bucketName);

            // Способ 2: DoesS3BucketExistV2Async (может врать)
            bool existsV2 = await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, bucketName);

            return Ok(new
            {
                BucketName = bucketName,
                ExistsInList = existsInList,
                ExistsV2 = existsV2,
                Message = existsInList
                    ? $"Bucket '{bucketName}' действительно существует"
                    : $"Bucket '{bucketName}' НЕ существует"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = ex.Message });
        }
    }
}