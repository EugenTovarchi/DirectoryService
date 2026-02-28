using Amazon.S3;
using FileService.Domain;
using SharedService.SharedKernel;

namespace FileService.Infrastructure.S3;

public static class S3ErrorMapper
{
    public static Error ToError(Exception error) => error switch
    {
        AmazonS3Exception { ErrorCode: "NoSuchBucket" } => FileErrors.BucketNotFound(),

        AmazonS3Exception
            {
                ErrorCode: "AccessDenied" or "SignatureDoesNotMatch" or "InvalidAccessKeyId"
            }

                => FileErrors.Denied(),

        AmazonS3Exception { ErrorCode: "InvalidRequest" or "InvalidArgument" } => FileErrors.ValidationFailed(),

        AmazonS3Exception { ErrorCode: "InternalError" } => FileErrors.InternalServerError(),

        AmazonS3Exception { ErrorCode: "NoSuchKey" } => FileErrors.ObjectNotFound(),

        AmazonS3Exception { ErrorCode: "NoSuchUpload" } => FileErrors.UploadNotFound(),

        AmazonS3Exception argumentException => FileErrors.ValidationFailed(),

        HttpRequestException => FileErrors.NetworkIssue(),

        OperationCanceledException => FileErrors.OperationCancelled(),

        _ => FileErrors.UnknownError(),
    };
}