using FileService.Domain;
using FileService.Domain.MediaProcessing.VO;
using FluentAssertions;

namespace FileService.UnitTests;

public class DomainValueObjectTests
{
    [Fact]
    public void VideoMetadata_With_Same_Values_Should_Be_Equal()
    {
        var first = VideoMetadata.Create(TimeSpan.FromSeconds(30), 1920, 1080, hasAudio: true).Value;
        var second = VideoMetadata.Create(TimeSpan.FromSeconds(30), 1920, 1080, hasAudio: true).Value;

        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Fact]
    public void FileName_Should_Trim_And_Validate_Lengths()
    {
        var fileName = FileName.Create("  Report.PDF  ").Value;

        fileName.Name.Should().Be("Report");
        fileName.Extension.Should().Be("pdf");
        fileName.Value.Should().Be("Report.pdf");

        FileName.Create($"{new string('a', FileName.NAME_MAX_LENGTH)}.toolong").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ContentType_Should_Trim_And_Validate_Length()
    {
        var contentType = ContentType.Create("  video/mp4  ").Value;

        contentType.Value.Should().Be("video/mp4");
        contentType.MediaType.Should().Be(MediaType.VIDEO);

        ContentType.Create(new string('a', ContentType.VALUE_MAX_LENGTH + 1)).IsFailure.Should().BeTrue();
    }
}
