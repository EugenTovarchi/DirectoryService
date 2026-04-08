namespace FileService.VideoProcessing.Preview;

public interface IPreviewCalculator
{
    List<TimeSpan> CalculateExtractionTimes(TimeSpan duration);
}