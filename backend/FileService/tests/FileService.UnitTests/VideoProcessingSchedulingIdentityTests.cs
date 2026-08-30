using FileService.Core.Abstractions;
using FluentAssertions;

namespace FileService.UnitTests;

public class VideoProcessingSchedulingIdentityTests
{
    [Fact]
    public void SchedulerContract_ShouldRequireVideoProcessId()
    {
        // Arrange
        var method = typeof(IVideoProcessingScheduler).GetMethod(
            nameof(IVideoProcessingScheduler.ScheduleProcessingAsync));

        // Act
        var parameters = method?.GetParameters();

        // Assert
        method.Should().NotBeNull();
        parameters.Should().NotBeNull();
        parameters![1].Name.Should().Be("videoProcessId");
        parameters[1].ParameterType.Should().Be<Guid>();
    }

    [Fact]
    public void RecoverableProcess_ShouldExposePersistedVideoProcessId()
    {
        // Arrange
        Type recoverableProcessType = typeof(RecoverableVideoProcess);

        // Act
        var property = recoverableProcessType.GetProperty("VideoProcessId");

        // Assert
        property.Should().NotBeNull();
    }
}
