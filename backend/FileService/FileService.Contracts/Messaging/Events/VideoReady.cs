namespace FileService.Contracts.Messaging.Events;

public sealed record VideoReady(
    Guid AssetId,
    Guid TargetEntityId,
    string TargetEntityType,
    string HlsKey,
    string CorrelationId,
    DateTimeOffset ReadyAtUtc);
