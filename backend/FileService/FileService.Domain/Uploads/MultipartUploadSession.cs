using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace FileService.Domain.Uploads;

/// <summary>
/// Хранит техническое состояние одной multipart-загрузки и служит общей
/// точкой координации для всех реплик FileService.
/// </summary>
public sealed class MultipartUploadSession
{
    public const int MAX_UPLOAD_ID_LENGTH = 1024;
    public const int MAX_IDEMPOTENCY_KEY_LENGTH = 128;
    public const int MAX_FAILURE_REASON_LENGTH = 2000;

    private MultipartUploadSession() { }

    public Guid Id { get; private set; }
    public Guid MediaAssetId { get; private set; }
    public string? UploadId { get; private set; }

    /// <summary>
    /// Клиентский ключ повторяемости запроса. Позволяет вернуть существующую
    /// сессию при повторном вызове start вместо создания второго S3 upload.
    /// </summary>
    public string? IdempotencyKey { get; private set; }

    /// <summary>Текущий этап multipart-загрузки.</summary>
    public MultipartUploadStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    /// <summary>
    /// Момент, после которого новые части принимать нельзя, а сессию может обработать фоновая очистка.
    /// </summary>
    public DateTime? ExpiresAt { get; private set; }

    /// <summary>
    /// Маркер версии для защиты от конкурирующих изменений.
    /// Меняется при каждом доменном переходе состояния.
    /// </summary>
    public Guid Version { get; private set; }
    public string? FailureReason { get; private set; }

    public static Result<MultipartUploadSession, Error> Create(
        Guid mediaAssetId,
        string? idempotencyKey = null)
    {
        if (mediaAssetId == Guid.Empty)
        {
            return Error.Validation(
                "multipart.upload.media_asset_id.invalid",
                "Media asset id is required");
        }

        string? normalizedIdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey)
            ? null
            : idempotencyKey.Trim();
        if (normalizedIdempotencyKey?.Length > MAX_IDEMPOTENCY_KEY_LENGTH)
        {
            return Error.Validation(
                "multipart.upload.idempotency_key.invalid",
                $"Idempotency key must not exceed {MAX_IDEMPOTENCY_KEY_LENGTH} characters");
        }

        DateTime now = DateTime.UtcNow;

        return new MultipartUploadSession
        {
            Id = Guid.NewGuid(),
            MediaAssetId = mediaAssetId,
            IdempotencyKey = normalizedIdempotencyKey,
            Status = MultipartUploadStatus.INITIALIZING,
            CreatedAt = now,
            UpdatedAt = now,
            Version = Guid.NewGuid()
        };
    }

    public UnitResult<Error> Activate(string uploadId, DateTime expiresAt)
    {
        if (Status != MultipartUploadStatus.INITIALIZING)
            return InvalidTransition(MultipartUploadStatus.ACTIVE);

        if (string.IsNullOrWhiteSpace(uploadId) || uploadId.Length > MAX_UPLOAD_ID_LENGTH)
        {
            return Error.Validation(
                "multipart.upload.id.invalid",
                $"Upload id is required and must not exceed {MAX_UPLOAD_ID_LENGTH} characters");
        }

        if (expiresAt <= DateTime.UtcNow)
        {
            return Error.Validation(
                "multipart.upload.expiration.invalid",
                "Multipart upload expiration must be in the future");
        }

        UploadId = uploadId;
        ExpiresAt = expiresAt;
        TransitionTo(MultipartUploadStatus.ACTIVE);

        return UnitResult.Success<Error>();
    }

    public UnitResult<Error> Complete()
    {
        if (Status != MultipartUploadStatus.COMPLETING)
            return InvalidTransition(MultipartUploadStatus.COMPLETED);

        TransitionTo(MultipartUploadStatus.COMPLETED);
        return UnitResult.Success<Error>();
    }

    /// <summary>
    /// Переводит активную сессию в промежуточное состояние перед внешним вызовом S3.
    /// В рабочем процессе такой переход выполняется атомарным обновлением в репозитории.
    /// </summary>
    public UnitResult<Error> BeginCompletion()
    {
        if (Status != MultipartUploadStatus.ACTIVE)
            return InvalidTransition(MultipartUploadStatus.COMPLETING);

        TransitionTo(MultipartUploadStatus.COMPLETING);
        return UnitResult.Success<Error>();
    }

    /// <summary>
    /// Возвращает сессию в ACTIVE после временной ошибки S3, чтобы запрос можно было повторить.
    /// </summary>
    public UnitResult<Error> ReleaseCompletion(string failureReason)
    {
        if (Status != MultipartUploadStatus.COMPLETING)
            return InvalidTransition(MultipartUploadStatus.ACTIVE);

        return ReturnToActive(failureReason);
    }

    public UnitResult<Error> Abort()
    {
        if (Status != MultipartUploadStatus.ABORTING)
            return InvalidTransition(MultipartUploadStatus.ABORTED);

        TransitionTo(MultipartUploadStatus.ABORTED);
        return UnitResult.Success<Error>();
    }

    /// <summary>
    /// Переводит активную сессию в промежуточное состояние перед отменой в S3.
    /// В рабочем процессе такой переход выполняется атомарным обновлением в репозитории.
    /// </summary>
    public UnitResult<Error> BeginAbort()
    {
        if (Status != MultipartUploadStatus.ACTIVE)
            return InvalidTransition(MultipartUploadStatus.ABORTING);

        TransitionTo(MultipartUploadStatus.ABORTING);
        return UnitResult.Success<Error>();
    }

    /// <summary>
    /// Возвращает сессию в ACTIVE после временной ошибки отмены.
    /// </summary>
    public UnitResult<Error> ReleaseAbort(string failureReason)
    {
        if (Status != MultipartUploadStatus.ABORTING)
            return InvalidTransition(MultipartUploadStatus.ACTIVE);

        return ReturnToActive(failureReason);
    }

    public UnitResult<Error> Fail(string failureReason)
    {
        if (Status is MultipartUploadStatus.COMPLETED or MultipartUploadStatus.ABORTED)
            return InvalidTransition(MultipartUploadStatus.FAILED);

        if (string.IsNullOrWhiteSpace(failureReason) || failureReason.Length > MAX_FAILURE_REASON_LENGTH)
        {
            return Error.Validation(
                "multipart.upload.failure_reason.invalid",
                $"Failure reason is required and must not exceed {MAX_FAILURE_REASON_LENGTH} characters");
        }

        FailureReason = failureReason;
        TransitionTo(MultipartUploadStatus.FAILED);

        return UnitResult.Success<Error>();
    }

    public UnitResult<Error> Expire(DateTime now)
    {
        if (Status != MultipartUploadStatus.ABORTING)
            return InvalidTransition(MultipartUploadStatus.EXPIRED);

        if (!ExpiresAt.HasValue || ExpiresAt.Value > now)
        {
            return Error.Validation(
                "multipart.upload.not_expired",
                "Multipart upload session has not expired");
        }

        TransitionTo(MultipartUploadStatus.EXPIRED);
        return UnitResult.Success<Error>();
    }

    private void TransitionTo(MultipartUploadStatus nextStatus)
    {
        Status = nextStatus;
        UpdatedAt = DateTime.UtcNow;
        Version = Guid.NewGuid();
    }

    private UnitResult<Error> ReturnToActive(string failureReason)
    {
        if (string.IsNullOrWhiteSpace(failureReason) || failureReason.Length > MAX_FAILURE_REASON_LENGTH)
        {
            return Error.Validation(
                "multipart.upload.failure_reason.invalid",
                $"Failure reason is required and must not exceed {MAX_FAILURE_REASON_LENGTH} characters");
        }

        FailureReason = failureReason;
        TransitionTo(MultipartUploadStatus.ACTIVE);
        return UnitResult.Success<Error>();
    }

    private UnitResult<Error> InvalidTransition(MultipartUploadStatus nextStatus)
    {
        return Error.Validation(
            "multipart.upload.status.invalid",
            $"Cannot transition multipart upload from {Status} to {nextStatus}");
    }
}
