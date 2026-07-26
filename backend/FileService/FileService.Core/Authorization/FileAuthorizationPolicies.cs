namespace FileService.Core.Authorization;

public static class FileAuthorizationPolicies
{
    public const string FILES_READ = "files.read";
    public const string FILES_UPLOAD = "files.upload";
    public const string FILES_DELETE = "files.delete";
    public const string FILE_SERVICE_INTERNAL = "file-service.internal";
}
