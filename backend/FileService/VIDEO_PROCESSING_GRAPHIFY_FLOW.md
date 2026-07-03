# VideoProcessing flow: Graphify-assisted разбор

Этот документ — отдельный вариант разбора VideoProcessing, построенный от графа Graphify и затем проверенный по исходному коду. Graphify использовался для поиска связанных классов, методов и переходов между слоями. Точное поведение подтверждено исходными файлами FileService.

## 1. Цель системы

Пользователь загружает исходное видео частями напрямую в S3-совместимое хранилище. После завершения multipart upload HTTP-запрос не ждёт ffmpeg, потому что транскодирование может занимать минуты и потреблять много CPU.

Поэтому обработка разделена на две части:

1. HTTP-flow подтверждает загрузку, сохраняет `VideoProcess` и планирует фоновую задачу.
2. Quartz выполняет тяжёлый pipeline, управляет повторными попытками и переживает перезапуск приложения.

Результатом обработки являются:

- HLS master playlist;
- несколько HLS-вариантов разрешения;
- `.ts`-сегменты;
- JPEG-превью;
- sprite sheet;
- статус готового видео;
- интеграционное событие `VideoReady`.

Главная идея:

```text
HTTP отвечает быстро
       ↓
надёжная Quartz job выполняет тяжёлую работу в фоне
       ↓
результат и событие сохраняются только после успешного pipeline
```

## 2. Главные классы, найденные Graphify

| Класс | Ответственность |
|---|---|
| `CompleteMultipartUploadHandler` | Завершает multipart upload, создаёт `VideoProcess`, выполняет commit и планирует job |
| `VideoProcess` | Хранит состояние обработки, шаги, progress, retry count и ошибки |
| `VideoProcessingScheduler` | Создаёт Quartz job и trigger, не допускает дублирования |
| `VideoProcessingJob` | Выполняет одну попытку, принимает решение о retry или permanent failure |
| `VideoProcessingService` | Обёртка над pipeline, общие логи, metrics и tracing |
| `ProcessingPipeline` | Последовательно запускает step handlers и сохраняет progress |
| `ProcessingErrorClassifier` | Делит ошибки на retryable и permanent |
| `ConfiguredVideoProcessingPolicy` | Возвращает `MaxRetries` и задержку retry |
| `VideoProcessingRecoveryService` | Ищет процессы без надёжно работающего trigger и восстанавливает scheduling |
| `TempDirectoryCleanupJob` | Удаляет оставшийся временный мусор |
| `VideoProcessingDependencyInjection` | Регистрирует pipeline и конфигурирует Quartz persistent store |

## 3. Полный flow

```text
StartMultipartUpload
        ↓
клиент загружает chunks напрямую в S3
        ↓
POST /files/multipart/end
        ↓
CompleteMultipartUploadHandler
        ├─ завершает multipart upload в S3
        ├─ MediaAsset: UPLOADING → UPLOADED
        ├─ сохраняет FileUploaded в Wolverine outbox
        ├─ создаёт VideoProcess(PENDING)
        └─ COMMIT PostgreSQL
                 ↓ только после успешного commit
        VideoProcessingScheduler
                 ↓
        persistent Quartz job + trigger
                 ↓
        VideoProcessingJob.Execute
                 ↓
        VideoProcessingService
                 ↓
        ProcessingPipeline
                 ├─ Initialize
                 ├─ ExtractMetadata
                 ├─ GenerateHls
                 ├─ UploadHls
                 ├─ GeneratePreview
                 └─ Cleanup
                 ↓
        VideoProcess: SUCCEEDED
        MediaAsset: READY
        VideoReady → Wolverine outbox
                 ↓
        RabbitMQ topic exchange
```

## 4. Завершение загрузки и граница транзакции

Точка входа:

```text
POST /files/multipart/end
```

`CompleteMultipartUploadHandler` открывает транзакцию PostgreSQL, загружает `MediaAsset`, проверяет количество частей и завершает multipart upload в S3.

Для видео создаётся доменный процесс:

```csharp
var createVideoProcessResult = VideoProcess.Create(
    mediaAsset.Id,
    mediaAsset.UploadKey,
    _videoProcessingPolicy.MaxRetries,
    correlationId);

_videoProcessesRepository.Add(createVideoProcessResult.Value);
await _transactionManager.SaveChangeAsync(cancellationToken);
```

Quartz планируется только после commit:

```csharp
var finalCommitResult = transactionScope.Commit();
if (finalCommitResult.IsFailure)
    return finalCommitResult.Error.ToFailure();

if (videoAssetIdToSchedule.HasValue)
{
    await _videoProcessingScheduler.ScheduleProcessingAsync(
        videoAssetIdToSchedule.Value,
        correlationId,
        startAt: null,
        cancellationToken: cancellationToken);
}
```

Почему это важно:

```text
Плохо:
Schedule job → job стартует → VideoProcess ещё не закоммичен

Правильно:
SaveChanges → Commit → Schedule job
```

Если scheduling после commit временно не сработает, HTTP-flow не может откатить уже успешно завершённую загрузку. Потерянную job позже восстановит `VideoProcessingRecoveryService`.

## 5. Что сохраняется в VideoProcess

`VideoProcess` — доменная state machine обработки:

```text
PENDING → RUNNING → SUCCEEDED
              ↓
            FAILED → PENDING → RUNNING
```

Основные данные:

- `VideoAssetId` — исходное видео;
- `RawKey` — ключ исходного объекта;
- `HlsKey` — ключ master playlist;
- `CorrelationId` — связь HTTP, job, pipeline и события;
- `Status` — состояние всего процесса;
- `Steps` — состояния отдельных шагов;
- `RetryCount` / `MaxRetries`;
- `NextRetryAt`;
- `IsCriticalError`;
- metadata: duration, width, height, `HasAudio`.

Шаги и их вес в общем progress:

| Шаг | Вес |
|---|---:|
| Initialize | 0% |
| ExtractMetadata | 10% |
| GenerateHls | 60% |
| UploadHls | 15% |
| GeneratePreview | 10% |
| Cleanup | 5% |

Состояние процесса и шагов хранится в `video_processes` и `video_processing_steps`. Поэтому после рестарта приложение знает, что обрабатывалось и где произошла ошибка.

## 6. Scheduler: зачем он нужен

`VideoProcessingScheduler` переводит бизнес-команду «обработать видео» в Quartz job.

Job получает стабильный ключ:

```csharp
var jobKey = new JobKey(
    $"VideoProcessing_{videoAssetId}",
    VideoProcessingScheduler.GROUP_NAME);
```

В `JobDataMap` сохраняются минимальные данные:

```csharp
.UsingJobData("VideoAssetId", videoAssetId.ToString())
.UsingJobData("AttemptNumber", "1")
.UsingJobData("CorrelationId", correlationId)
```

Сам job durable и recovery-aware:

```csharp
.StoreDurably()
.RequestRecovery()
```

Trigger использует:

```csharp
.WithSimpleSchedule(schedule =>
    schedule.WithMisfireHandlingInstructionFireNow())
```

Если приложение не работало в запланированный момент, Quartz запускает просроченный trigger после восстановления.

Scheduler также проверяет существующий `JobKey`. Если job уже имеет trigger или выполняется, повторное планирование считается успешным no-op. Это делает scheduling идемпотентным.

## 7. Ограничение конкурентности

Есть два уровня защиты.

### Общий лимит

```csharp
q.UseDefaultThreadPool(options =>
    options.MaxConcurrency = videoOptions.MaxConcurrentJobs);
```

По умолчанию одновременно выполняются не более двух Quartz jobs. Это защищает CPU и память от множества параллельных ffmpeg.

### Защита одной job

```csharp
[DisallowConcurrentExecution]
public class VideoProcessingJob : IJob
```

Quartz не выполняет одновременно две копии job с одинаковым `JobKey`. Разные видео могут обрабатываться параллельно в пределах общего лимита.

## 8. Выполнение VideoProcessingJob

При старте попытки job:

1. читает `VideoAssetId`, `AttemptNumber`, `CorrelationId`;
2. создаёт logging scope;
3. создаёт tracing activity;
4. проверяет наличие `MediaAsset`;
5. проверяет допустимый статус `UPLOADED` или `PROCESSING`;
6. загружает `VideoProcess`;
7. завершает job, если процесс уже `SUCCEEDED` или permanent failed;
8. восстанавливает прерванный `RUNNING` процесс;
9. подготавливает `FAILED` процесс к retry;
10. вызывает `VideoProcessingService.ProcessVideoAsync`.

Logging scope:

```csharp
using IDisposable? logScope = _logger.BeginScope(
    new Dictionary<string, object>
    {
        ["CorrelationId"] = correlationId,
        ["VideoAssetId"] = videoAssetId,
    });
```

Это добавляет идентификаторы ко всем вложенным логам без передачи их в каждый вызов `LogInformation`.

## 9. Pipeline и его шаги

`ProcessingPipeline` загружает `VideoAsset` и `VideoProcess`, переводит asset в `PROCESSING`, а затем в цикле берёт следующий `PENDING` step.

Общий алгоритм:

```csharp
while (true)
{
    Result<VideoProcessStep?, Error> stepResult =
        processingContext.VideoProcess.ProcessNextStep();

    if (stepResult.Value is null)
        break;

    IProcessingStepHandler handler = FindHandler(stepResult.Value.Name);
    Result<ProcessingContext, Error> result =
        await handler.ExecuteAsync(processingContext, cancellationToken);

    processingContext.VideoProcess.CompleteStep(
        processingContext.VideoProcess.CurrentStep!.Order);

    await _transactionManager.SaveChangeAsync(cancellationToken);
}
```

После каждого шага progress сохраняется в PostgreSQL.

### 9.1 Initialize

Создаёт уникальную рабочую директорию в системном temp. В ней будут HLS-файлы, previews и sprite sheet.

### 9.2 ExtractMetadata

Создаёт короткоживущий presigned download URL и запускает `ffprobe`:

```text
ffprobe
  -show_entries stream=codec_type,width,height
  -show_entries format=duration
  -of json
```

В `VideoMetadata` сохраняются:

- duration;
- width;
- height;
- наличие audio stream.

Presigned URL не записывается в обычные логи.

### 9.3 GenerateHls

`FfmpegProcessRunner` выбирает только разрешения, которые не превышают высоту исходного видео:

```csharp
Rendition[] renditions = _renditions
    .Where(rendition => rendition.Height <= sourceHeight)
    .ToArray();
```

Поддерживаемые варианты:

```text
360p  → 2 Mbps
720p  → 3 Mbps
1080p → 5 Mbps
```

Для низкого исходника создаётся один вариант с исходной высотой. 1080p не генерируется из 720p.

Если metadata показывает отсутствие audio stream, ffmpeg не получает `-map 0:a:0`. Поэтому видео без аудио тоже обрабатывается.

HLS параметры:

- segment duration — 4 секунды;
- playlist type — VOD;
- segment type — MPEG-TS;
- master playlist — `master.m3u8`;
- отдельный stream playlist для каждого rendition.

### 9.4 UploadHls

Все `.m3u8` и `.ts` из рабочей директории загружаются в storage.

Параллельность ограничивается:

```csharp
using var throttler = new SemaphoreSlim(
    _options.UploadDegreeOfParallelism);
```

После upload `VideoAsset` и `VideoProcess` получают ключ master playlist.

### 9.5 GeneratePreview

На основании duration рассчитываются timestamps. Для каждого timestamp ffmpeg извлекает JPEG, затем создаётся sprite sheet через `xstack`.

Результаты загружаются в preview storage, а ключи сохраняются в `ProcessingContext`.

### 9.6 Cleanup

Cleanup пытается:

- удалить raw video из storage;
- рекурсивно удалить локальную рабочую директорию;
- очистить временные значения `ProcessingContext`.

Ошибка удаления временной директории не ломает успешно созданный HLS. Старый мусор позже удалит отдельная cleanup job.

## 10. Завершение обработки и VideoReady

После всех шагов pipeline проверяет:

- `HlsKey` существует;
- все steps имеют `SUCCEEDED`;
- процесс находится в `RUNNING`;
- asset находится в `PROCESSING`.

Затем создаётся событие:

```csharp
var videoReadyEvent = new VideoReady(
    context.VideoAsset.Id,
    context.VideoAsset.OwnerId,
    context.VideoAsset.OwnerType,
    context.VideoProcess.HlsKey.Value,
    context.VideoProcess.CorrelationId,
    DateTimeOffset.UtcNow);
```

После публикации в Wolverine:

```text
VideoProcess → SUCCEEDED
MediaAsset   → READY
VideoReady   → durable outbox
```

Эти изменения сохраняются одним `SaveChangesAsync`. Wolverine durable outbox отправит событие в RabbitMQ после фиксации БД.

Важно различать события:

- `FileUploaded` означает, что raw upload завершён;
- `VideoReady` означает, что HLS действительно создан и доступен.

## 11. Retry flow

### 11.1 Классификация ошибки

Pipeline передаёт `Error` в `ProcessingErrorClassifier`.

Permanent считаются:

- `VALIDATION`;
- `NOT_FOUND`;
- `CONFLICT`;
- известные коды повреждённого input или нарушенного pipeline invariant.

Примеры permanent codes:

```text
ffprobe.invalid.output
hls.processing.failed
pipeline.handler.not.found
asset.invalid.status
asset.invalid.status.transition
```

Неожиданное исключение по умолчанию считается временным.

### 11.2 Неуспешный step

```text
Step handler вернул Error
        ↓
VideoProcess.Fail(message, isCritical)
        ↓
статус FAILED сохраняется в PostgreSQL
        ↓
ошибка возвращается в VideoProcessingJob
```

### 11.3 Решение Job

```csharp
public bool CanRetry() =>
    RetryCount < MaxRetries && !IsCriticalError;
```

Если retry запрещён, job помечает процесс permanent failed и удаляется.

Если retry разрешён:

1. вычисляется delay;
2. `NextRetryAt` сохраняется в `VideoProcess`;
3. создаётся новый trigger для той же durable job;
4. job не удаляется.

### 11.4 Backoff

Для `RetryDelaySeconds = 60`:

| RetryCount | Множитель | Задержка |
|---:|---:|---:|
| 0 | 1 | 60 секунд |
| 1 | 2 | 120 секунд |
| 2 | 4 | 240 секунд |
| 3+ | 8 | 480 секунд |

Задержка ограничена множителем 8, чтобы очередь не откладывала обработку бесконечно далеко.

### 11.5 Следующая попытка

Когда retry trigger срабатывает, `PrepareForRetry()`:

```csharp
foreach (VideoProcessStep step in _steps)
{
    step.Reset();
}

RetryCount++;
NextRetryAt = null;
Status = VideoProcessStatus.PENDING;
```

Pipeline начинается с `Initialize`, а не с упавшего шага. Причина: `ProcessingContext`, presigned URL и локальные файлы существуют только внутри одного Quartz execution и не переживают рестарт процесса.

## 12. Что происходит при исключении

`VideoProcessingJob` ловит исключение верхнего уровня:

```text
Exception
  ↓
VideoProcess.Fail(..., isCritical: false)
  ↓
SaveChanges
  ↓
обычный retry flow
```

Таким образом случайный network timeout, временная ошибка S3 или сбой процесса не завершают workflow навсегда с первой попытки.

## 13. Persistent Quartz store

При `UsePersistentStore = true` Quartz хранит job, trigger и scheduler state в PostgreSQL schema `quartz`.

Основные группы таблиц:

- `qrtz_job_details` — описание durable jobs и `JobDataMap`;
- `qrtz_triggers` — общие данные triggers;
- `qrtz_simple_triggers` — retry/start triggers с конкретным временем;
- `qrtz_fired_triggers` — выполняющиеся triggers;
- `qrtz_scheduler_state` — heartbeat экземпляров scheduler;
- `qrtz_locks` — координация нескольких инстансов;
- остальные trigger tables — стандартные типы Quartz, даже если сейчас используются не все.

Persistent store решает ситуацию:

```text
job запланирована
        ↓
FileService остановился
        ↓
job и trigger остались в PostgreSQL
        ↓
FileService запустился
        ↓
Quartz продолжил выполнение
```

Clustering позволяет нескольким FileService использовать одну Quartz DB. Job забирает только один scheduler instance.

## 14. Startup recovery

Persistent Quartz защищает уже записанные jobs. Но остаётся окно:

```text
VideoProcess commit выполнен
        ↓
процесс завершился до ScheduleJob
```

Для этого существует `VideoProcessingRecoveryService`.

Каждые `RecoveryScanIntervalSeconds` он выбирает:

- `PENDING`;
- `RUNNING`;
- retryable `FAILED`, у которых не исчерпаны retries.

Для каждого кандидата вызывается идемпотентный scheduler. Если job уже существует, дубликат не создаётся. Если trigger потерян, scheduler добавляет recovery trigger.

`BackgroundService` является singleton, поэтому repository и scheduler получаются из нового async scope:

```csharp
await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

var repository = scope.ServiceProvider
    .GetRequiredService<IVideoProcessesRepository>();
```

Так каждый scan получает новый scoped `DbContext`.

## 15. Temp cleanup job

`TempDirectoryCleanupJob` запускается Quartz по интервалу и удаляет каталоги `video-processing*`, которые старше `TempDirectoryMaxAgeHours`.

`[DisallowConcurrentExecution]` не позволяет двум cleanup execution одновременно обходить одни директории.

`DirectoryNotFoundException` игнорируется намеренно: другой cleanup уже мог удалить ту же директорию, и требуемое состояние уже достигнуто.

## 16. Observability

### Logs

В job scope находятся:

- `CorrelationId`;
- `VideoAssetId`.

Ошибки логируются с отдельным structured `ErrorCode`.

ffmpeg stderr:

- не дублируется;
- URL заменяются на `[REDACTED_URL]`;
- вывод ограничен 4096 символами.

### Metrics

```text
video_processing.active_jobs
video_processing.completed_jobs
video_processing.failed_attempts
video_processing.retried_jobs
video_processing.duration
video_processing.step.duration
```

`CorrelationId` и `VideoAssetId` не используются как metric labels, чтобы не создавать high cardinality в Prometheus.

### Traces

Создаются activities:

```text
video.processing.job
video.processing
video.processing.step.<StepName>
```

## 17. Основные настройки

| Настройка | Назначение | Development |
|---|---|---:|
| `FfmpegPath` | путь к ffmpeg | локальный exe |
| `FfprobePath` | путь к ffprobe | локальный exe |
| `VideoEncoder` | video codec | `libx264` |
| `VideoPreset` | speed/quality preset | `medium` |
| `UploadDegreeOfParallelism` | параллельный upload HLS | 3 |
| `MaxRetries` | максимум retries | 3 |
| `RetryDelaySeconds` | базовая задержка | 60 |
| `MaxConcurrentJobs` | общий лимит Quartz jobs | 2 |
| `UsePersistentStore` | хранить Quartz в PostgreSQL | true |
| `UseQuartzClustering` | координация инстансов | true |
| `RecoveryScanIntervalSeconds` | период reconciliation | 60 |
| `TempDirectoryMaxAgeHours` | возраст мусора | 24 |
| `TempCleanupIntervalMinutes` | период cleanup | 60 |

Options проверяются через `IValidateOptions<T>` и `ValidateOnStart`. Некорректная конфигурация останавливает приложение при запуске, а не проявляется во время первой job.

В Docker image отдельно устанавливается `ffmpeg`; пакет содержит и `ffprobe`.

## 18. Надёжность по слоям

```text
PostgreSQL transaction
  защищает MediaAsset, VideoProcess и Wolverine outbox

Quartz persistent store
  защищает job и trigger при рестарте

RecoveryService
  восстанавливает разрыв между commit и ScheduleJob

DisallowConcurrentExecution + MaxConcurrentJobs
  защищают от дублей и перегрузки ffmpeg

Retry policy + classifier
  повторяют временные ошибки и останавливают permanent failures

TempDirectoryCleanupJob
  удаляет мусор после crash
```

## 19. Важный текущий риск

`Cleanup` удаляет raw video до `FinalizeAsync`. Если публикация `VideoReady` завершится исключением после успешного cleanup, процесс станет `FAILED`, а retry начнётся с `Initialize`. При этом исходный raw object уже может отсутствовать.

Это редкое, но реальное окно. Более строгий вариант архитектуры:

1. все преобразования завершены;
2. final state и `VideoReady` записаны в outbox;
3. raw удаляется отдельной идемпотентной cleanup job после commit.

Текущая реализация не требует немедленного изменения для понимания flow, но этот риск стоит учитывать при дальнейшем усилении надёжности.

## 20. Как объяснить на собеседовании

Краткий вариант:

> После завершения multipart upload я сохраняю доменный `VideoProcess` и только после commit планирую durable Quartz job. Quartz использует PostgreSQL persistent store, поэтому job и triggers переживают рестарт, а reconciliation service восстанавливает редкое окно между commit и scheduling. Job запускает пошаговый ffmpeg pipeline, сохраняет progress после каждого этапа, классифицирует ошибки и создаёт retry trigger с exponential backoff. После успешного HLS и previews состояние видео и событие `VideoReady` сохраняются через transactional outbox. Параллельность ffmpeg ограничена, а старые временные директории удаляются отдельной cleanup job.

## 21. Источники, проверенные после Graphify

- `FileService.Core/Features/CompleteMultipartUploadEndpoint.cs`
- `FileService.Domain/MediaProcessing/VideoProcess.cs`
- `FileService.VideoProcessing/Quartz/VideoProcessingScheduler.cs`
- `FileService.VideoProcessing/Quartz/VideoProcessingJob.cs`
- `FileService.VideoProcessing/Quartz/VideoProcessingRecoveryService.cs`
- `FileService.VideoProcessing/Quartz/TempDirectoryCleanupJob.cs`
- `FileService.VideoProcessing/Pipeline/ProcessingPipeline.cs`
- `FileService.VideoProcessing/Pipeline/StepHandlers/*`
- `FileService.VideoProcessing/FfmpegProcess/FfmpegProcessRunner.cs`
- `FileService.VideoProcessing/ProcessRunner/DataProcessRunner.cs`
- `FileService.VideoProcessing/VideoProcessingDependencyInjection.cs`
- `FileService.Contracts/Messaging/Events/VideoReady.cs`
- `FileService.Contracts/Messaging/RabbitMqConfiguration.cs`
