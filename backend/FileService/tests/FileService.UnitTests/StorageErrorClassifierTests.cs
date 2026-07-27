using FileService.Core.FilesStorage;
using FluentAssertions;
using SharedService.SharedKernel;

namespace FileService.UnitTests;

public sealed class StorageErrorClassifierTests
{
    // [Theory] используется для параметризованного теста:
    // один метод запускается несколько раз с разными входными данными.
    //
    // Каждый [InlineData] ниже создаёт отдельный тестовый случай.
    // Например, [InlineData("network.issue")] передаст строку "network.issue"
    // в параметр errorCode метода IsRetryable_WithTemporaryStorageError_ShouldReturnTrue.
    //
    // В результате xUnit выполнит этот метод четыре раза и отдельно покажет,
    // для какого errorCode тест прошёл или завершился ошибкой.
    [Theory]
    [InlineData("network.issue")]
    [InlineData("internal.server.error")]
    [InlineData("operation.cancelled")]
    [InlineData("unknown.error")]
    public void IsRetryable_WithTemporaryStorageError_ShouldReturnTrue(string errorCode)
    {
        Error error = Error.Failure(errorCode, "Temporary storage error");

        bool result = StorageErrorClassifier.IsRetryable(error);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsRetryable_WithPermanentStorageError_ShouldReturnFalse()
    {
        Error error = Error.Failure("upload.id", "Multipart upload does not exist");

        bool result = StorageErrorClassifier.IsRetryable(error);

        result.Should().BeFalse();
    }
}
