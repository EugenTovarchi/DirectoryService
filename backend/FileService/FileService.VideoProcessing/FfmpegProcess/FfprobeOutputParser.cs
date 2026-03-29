using System.Text.Json;
using System.Text.Json.Serialization;
using CSharpFunctionalExtensions;
using FileService.Domain;
using FileService.Domain.MediaProcessing.VO;
using SharedService.SharedKernel;

namespace FileService.VideoProcessing.FfmpegProcess;

public static class FfprobeOutputParser
{
    public static Result<VideoMetadata, Error> Parse(string jsonOutput)
    {
        if (string.IsNullOrWhiteSpace(jsonOutput))
            return FileErrors.InvalidFfprobeOutput("Empty json output.");

        FfprobeResponse? response;
        try
        {
            response = JsonSerializer.Deserialize<FfprobeResponse>(jsonOutput);
        }
        catch (JsonException ex)
        {
            return FileErrors.InvalidFfprobeOutput("Json parse error: " + ex.Message);
        }

        if (response is null)
            return FileErrors.InvalidFfprobeOutput("Null response");

        StreamInfo? stream = response.Streams?.FirstOrDefault();
        if (stream is null)
            return FileErrors.InvalidFfprobeOutput("No video stream found");

        if (stream.Width is null || stream.Height is null)
            return FileErrors.InvalidFfprobeOutput("Missing resolution");

        double? durationSeconds = response.Format?.Duration;
        if (durationSeconds is null || durationSeconds <= 0)
            return FileErrors.InvalidFfprobeOutput("Missing or invalid duration");

        var duration = TimeSpan.FromSeconds(durationSeconds.Value);

        return VideoMetadata.Create(duration, stream.Width.Value, stream.Height.Value);
    }
}

public sealed class FfprobeResponse
{
    [JsonPropertyName("streams")]
    public List<StreamInfo?> Streams { get; set; }

    [JsonPropertyName("format")]
    public FormatInfo? Format { get; set; }
}

public sealed class StreamInfo
{
    [JsonPropertyName("width")]
    public int? Width { get; set; }

    [JsonPropertyName("height")]
    public int? Height { get; set; }
}

public sealed class FormatInfo
{
    [JsonPropertyName("duration")]
    [JsonConverter(typeof(StringToDoubleConverter))]
    public double? Duration { get; set; }
}

public sealed class StringToDoubleConverter : JsonConverter<double?>
{
    public override double? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            string? str = reader.GetString();
            if (double.TryParse(str, out double result))
                return result;

            return null;
        }

        if (reader.TokenType == JsonTokenType.Number)
            return reader.GetDouble();

        return null;
    }

    public override void Write(Utf8JsonWriter writer, double? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteNumberValue(value.Value);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}