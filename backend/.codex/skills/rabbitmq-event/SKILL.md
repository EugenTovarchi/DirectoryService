---
name: rabbitmq-event
description: Use when adding or changing RabbitMQ/Wolverine integration events between FileService and DirectoryService.
---

# RabbitMQ Event Change

## Checklist

1. Identify producer service.
2. Identify consumer service.
3. Define event contract.
4. Include:
   - `CorrelationId`
   - business ids
   - timestamp if useful
5. Configure exchange/routing key/queue according to existing Wolverine setup.
6. Ensure producer logs before/after publish.
7. Ensure consumer logs receive/success/failure.
8. Ensure consumer is idempotent if event can be retried.
9. Consider outbox if event must not be lost after DB commit.

## Naming

Events should be past tense:

- `FileUploaded`
- `FileDeleted`
- `VideoHlsProcessed`
- `DepartmentVideoAssigned`

## Observability

Important log properties:

- `CorrelationId`
- `MessageId`
- `RawVideoId`
- `HlsVideoId`
- `DepartmentId`
- `RoutingKey`
- `QueueName`

## Verification

- Check RabbitMQ management UI.
- Check producer logs.
- Check consumer logs.
- Use Loki query:

```logql
{service_name=~"FileService|DirectoryService"} | json | CorrelationId="..."
```
