using System.Text.Json;
using FileService.Contracts.Messaging.Events;
using FileService.Domain.MediaProcessing;
using FluentAssertions;

namespace FileService.UnitTests;

public class VideoProcessingIdentityTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void VideoProcess_ShouldUseIdAsItsOnlyProcessingIdentity()
    {
        // Arrange
        Type videoProcessType = typeof(VideoProcess);

        // Act
        var property = videoProcessType.GetProperty("CorrelationId");

        // Assert
        property.Should().BeNull();
    }

    [Fact]
    public void VideoReady_ShouldDeserializeLegacyPayloadWithCorrelationId()
    {
        // Arrange
        Guid assetId = Guid.NewGuid();
        Guid targetEntityId = Guid.NewGuid();
        const string legacyCorrelationId = "legacy-correlation";
        string json = $$"""
            {
              "assetId": "{{assetId}}",
              "targetEntityId": "{{targetEntityId}}",
              "targetEntityType": "Department",
              "hlsKey": "hls/video/master.m3u8",
              "correlationId": "{{legacyCorrelationId}}",
              "readyAtUtc": "2026-08-19T10:00:00+00:00"
            }
            """;

        // Act
        VideoReady? message = JsonSerializer.Deserialize<VideoReady>(json, JsonOptions);

        // Assert
        message.Should().NotBeNull();
        message!.AssetId.Should().Be(assetId);
        message.CorrelationId.Should().Be(legacyCorrelationId);
    }

    [Fact]
    public void VideoReady_ShouldDeserializeNewPayloadWithoutLegacyCorrelationId()
    {
        // Arrange
        string json = $$"""
            {
              "assetId": "{{Guid.NewGuid()}}",
              "targetEntityId": "{{Guid.NewGuid()}}",
              "targetEntityType": "Department",
              "hlsKey": "hls/video/master.m3u8",
              "readyAtUtc": "2026-08-19T10:00:00+00:00"
            }
            """;

        // Act
        VideoReady? message = JsonSerializer.Deserialize<VideoReady>(json, JsonOptions);

        // Assert
        message.Should().NotBeNull();
        message!.CorrelationId.Should().BeNull();
    }
}
