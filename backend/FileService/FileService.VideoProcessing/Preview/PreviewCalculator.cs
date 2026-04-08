using FileService.VideoProcessing.Pipeline.Options;
using Microsoft.Extensions.Options;

namespace FileService.VideoProcessing.Preview;

public class PreviewCalculator : IPreviewCalculator
{
    private readonly PreviewOptions _options;

    public PreviewCalculator(IOptions<PreviewOptions> options)
    {
        _options = options.Value;
    }

    public List<TimeSpan> CalculateExtractionTimes(TimeSpan duration)
    {
        var times = new List<TimeSpan>();
        int previewCount = GetPreviewCount(duration);

        if (previewCount <= 0)
            return times;

        // Не берем начало и конец, норм точки видео получаем
        double interval = duration.TotalSeconds / (previewCount + 1);

        for (int i = 1; i <= previewCount; i++)
        {
            times.Add(TimeSpan.FromSeconds(interval * i));
        }

        return times;
    }

    private int GetPreviewCount(TimeSpan duration)
    {
        int totalSeconds = (int)duration.TotalSeconds;

        // Для очень коротких видео (меньше 10 секунд) - минимум превью
        if (totalSeconds <= 10)
            return Math.Min(_options.MinPreviewCount, totalSeconds / 2);

        // Для коротких видео (до 30 сек) - 3 превью
        if (totalSeconds <= 30)
            return Math.Min(_options.MaxPreviewCount, 3);

        // Для средних видео (до 60 сек) - 5 превью
        if (totalSeconds <= 60)
            return Math.Min(_options.MaxPreviewCount, 5);

        // Для длинных видео (до 5 минут) - 8 превью
        if (totalSeconds <= 300)
            return Math.Min(_options.MaxPreviewCount, 8);

        // Для очень длинных видео делим длительность на 30с
        int calculatedCount = totalSeconds / _options.SecondsPerPreview;

        return Math.Clamp(calculatedCount, _options.MinPreviewCount, _options.MaxPreviewCount);
    }
}