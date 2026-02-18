using Amazon.S3;
using Microsoft.Extensions.Options;

namespace FileService.Infrastructure.S3;

public class S3Provider
{
    private readonly IAmazonS3 _s3Client;
    private readonly S3Options _s3Options;

    public S3Provider(IAmazonS3 s3Client, IOptions<S3Options> s3Options)
    {
        _s3Client = s3Client;
        _s3Options = s3Options.Value;
    }

}