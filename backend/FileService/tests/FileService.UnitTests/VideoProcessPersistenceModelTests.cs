using FileService.Domain.MediaProcessing;
using FileService.Infrastructure.Postgres;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FileService.UnitTests;

public class VideoProcessPersistenceModelTests
{
    [Fact]
    public void CurrentModel_ShouldNotMapLegacyCorrelationId()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<FileServiceDbContext>()
            .UseNpgsql("Host=localhost;Database=model-only;Username=model-only;Password=model-only")
            .Options;
        using var dbContext = new FileServiceDbContext(options);

        // Act
        var entityType = dbContext.Model.FindEntityType(typeof(VideoProcess));

        // Assert
        entityType.Should().NotBeNull();
        entityType!.FindProperty("CorrelationId").Should().BeNull();
        entityType.GetProperties()
            .Should().NotContain(property => property.GetColumnName() == "correlation_id");
    }
}
