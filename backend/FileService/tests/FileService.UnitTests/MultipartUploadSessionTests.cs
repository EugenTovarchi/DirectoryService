using CSharpFunctionalExtensions;
using FileService.Domain.Uploads;
using FluentAssertions;
using SharedService.SharedKernel;

namespace FileService.UnitTests;

public sealed class MultipartUploadSessionTests
{
    [Fact]
    public void Create_WithValidMediaAsset_ShouldCreateInitializingSession()
    {
        // Act
        Result<MultipartUploadSession, Error> result = MultipartUploadSession.Create(Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(MultipartUploadStatus.INITIALIZING);
        result.Value.UploadId.Should().BeNull();
        result.Value.Version.Should().NotBeEmpty();
    }

    [Fact]
    public void Activate_WhenInitializing_ShouldSetUploadDataAndChangeVersion()
    {
        // Arrange
        MultipartUploadSession session = CreateSession();
        Guid initialVersion = session.Version;
        DateTime expiresAt = DateTime.UtcNow.AddHours(24);

        // Act
        UnitResult<Error> result = session.Activate("upload-id", expiresAt);

        // Assert
        result.IsSuccess.Should().BeTrue();
        session.Status.Should().Be(MultipartUploadStatus.ACTIVE);
        session.UploadId.Should().Be("upload-id");
        session.ExpiresAt.Should().Be(expiresAt);
        session.Version.Should().NotBe(initialVersion);
    }

    [Fact]
    public void Complete_WhenSessionWasNotClaimed_ShouldFail()
    {
        // Arrange
        MultipartUploadSession session = CreateActiveSession();

        // Act
        UnitResult<Error> result = session.Complete();

        // Assert
        result.IsFailure.Should().BeTrue();
        session.Status.Should().Be(MultipartUploadStatus.ACTIVE);
    }

    [Fact]
    public void Abort_WhenSessionWasNotClaimed_ShouldFail()
    {
        // Arrange
        MultipartUploadSession session = CreateActiveSession();

        // Act
        UnitResult<Error> result = session.Abort();

        // Assert
        result.IsFailure.Should().BeTrue();
        session.Status.Should().Be(MultipartUploadStatus.ACTIVE);
    }

    [Fact]
    public void Expire_BeforeExpiration_ShouldFail()
    {
        // Arrange
        MultipartUploadSession session = CreateActiveSession();

        // Act
        UnitResult<Error> result = session.Expire(DateTime.UtcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        session.Status.Should().Be(MultipartUploadStatus.ACTIVE);
    }

    [Fact]
    public void Complete_AfterCompletionClaim_ShouldCompleteSession()
    {
        // Arrange
        MultipartUploadSession session = CreateActiveSession();
        session.BeginCompletion();

        // Act
        UnitResult<Error> result = session.Complete();

        // Assert
        result.IsSuccess.Should().BeTrue();
        session.Status.Should().Be(MultipartUploadStatus.COMPLETED);
    }

    [Fact]
    public void Abort_AfterAbortClaim_ShouldAbortSession()
    {
        // Arrange
        MultipartUploadSession session = CreateActiveSession();
        session.BeginAbort();

        // Act
        UnitResult<Error> result = session.Abort();

        // Assert
        result.IsSuccess.Should().BeTrue();
        session.Status.Should().Be(MultipartUploadStatus.ABORTED);
    }

    [Fact]
    public void Expire_AfterAbortClaimAndExpiration_ShouldExpireSession()
    {
        // Arrange
        MultipartUploadSession session = CreateActiveSession();
        session.BeginAbort();

        // Act
        UnitResult<Error> result = session.Expire(DateTime.UtcNow.AddDays(1));

        // Assert
        result.IsSuccess.Should().BeTrue();
        session.Status.Should().Be(MultipartUploadStatus.EXPIRED);
    }

    private static MultipartUploadSession CreateSession()
    {
        return MultipartUploadSession.Create(Guid.NewGuid()).Value;
    }

    private static MultipartUploadSession CreateActiveSession()
    {
        MultipartUploadSession session = CreateSession();
        session.Activate("upload-id", DateTime.UtcNow.AddHours(24));
        return session;
    }
}