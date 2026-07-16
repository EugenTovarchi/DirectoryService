# gRPC Service Token Flow Guide

Дата среза: 2026-07-16.

Этот документ - полноценный путеводитель по текущему service-to-service flow между `DirectoryService`, `AuthService` и `FileService`.

Главная идея: внутренний gRPC endpoint не должен быть анонимным. Даже если сервисы живут в одной Docker-сети или на одной VM, принимающий сервис должен понимать, кто его вызывает и какое internal permission (внутреннее право сервиса) у caller service есть.

## 1. Какие сервисы участвуют

| Сервис | Роль |
| --- | --- |
| `DirectoryService` | Consumer service. Ему нужно проверить media asset или получить metadata у FileService. |
| `FileService.Contracts` | Adapter package. Прячет gRPC и получение token за `IFileCommunicationService`. |
| `AuthService` | Issuer service tokens. Выдает service JWT по client credentials. |
| `FileService` | Provider service. Владеет media assets и защищает internal gRPC endpoint. |

## 2. Полный flow

```text
DirectoryService handler
  -> IFileCommunicationService
  -> FileService.Contracts adapter
  -> AuthService /api/auth/service-token
  -> AuthService возвращает envelope: result.accessToken
  -> adapter кладет Authorization: Bearer <service-token> в gRPC metadata
  -> FileService gRPC endpoint проверяет JWT
  -> FileService policy проверяет service_permission=file-service.internal
  -> FileService выполняет internal method
  -> adapter переводит gRPC reply обратно в DirectoryService response
```

## 3. Почему не используем user JWT

User JWT отвечает на вопрос:

```text
Какой пользователь делает запрос и какие user permissions у него есть?
```

Service JWT отвечает на другой вопрос:

```text
Какой backend-сервис делает internal request и какие service permissions у него есть?
```

Это разные identities.

Для внутреннего gRPC endpoint нужен:

```text
service_permission = file-service.internal
```

А user permission вроде:

```text
permission = files.read
```

не подходит. `files.read` означает право пользователя читать файловые данные через user-facing API, а `file-service.internal` означает право backend-сервиса вызвать внутренний gRPC method.

## 4. Что это решило

- Internal gRPC endpoint больше не анонимный.
- User permissions не смешиваются с service permissions.
- `FileService` сам проверяет boundary (границу доступа) на своем endpoint.
- `DirectoryService` не знает про token, gRPC metadata или URL AuthService.
- `FileService.Contracts` остается adapter layer между business code и transport/security деталями.
- Service token кэшируется, поэтому каждый gRPC call не ходит заново в `AuthService`.

## 5. Как AuthService выдает service token

AuthService имеет endpoint:

```text
POST /api/auth/service-token
```

Запрос содержит client credentials:

```json
{
  "clientId": "directory-service",
  "clientSecret": "secret"
}
```

AuthService проверяет `ServiceClients` configuration:

```json
"ServiceClients": {
  "Clients": [
    {
      "ClientId": "directory-service",
      "ClientSecret": "local-dev-directory-service-client-secret-change-before-production",
      "ServiceName": "DirectoryService",
      "ServicePermissions": [
        "file-service.internal"
      ]
    }
  ]
}
```

Если client id и secret валидны, AuthService выпускает JWT с service claims.

## 6. Какие claims есть в service JWT

| Claim | Пример | Зачем |
| --- | --- | --- |
| `sub` | `directory-service` | Основная identity service client. |
| `jti` | random guid | Уникальный id token для логов и будущего deny-list. |
| `client_id` | `directory-service` | Технический id клиента. |
| `service_name` | `DirectoryService` | Человекочитаемое имя сервиса. |
| `service_permission` | `file-service.internal` | Внутреннее право сервиса. |

Важно: service token не содержит user claim `permission`.

## 7. Как DirectoryService получает token

Business code `DirectoryService` не вызывает AuthService напрямую.

Он работает так:

```csharp
Result<CheckMediaAssetExistResponse, Failure> existResult =
    await _fileCommunicationService.CheckMediaAssetExists(videoId, cancellationToken);
```

`IFileCommunicationService` реализован adapter-ом из `FileService.Contracts`.

Внутри adapter-а:

1. `AuthServiceTokenProvider` получает service token.
2. `FileCommunicationClient` добавляет token в gRPC metadata.
3. Generated gRPC client вызывает `FileService`.

Схема:

```text
DirectoryService handler
  -> IFileCommunicationService
  -> FileCommunicationClient
  -> AuthServiceTokenProvider
  -> AuthService /api/auth/service-token
  -> FileInternal gRPC client
  -> FileService
```

## 8. Как читать response envelope AuthService

AuthService возвращает token не прямым JSON-объектом, а внутри общего envelope:

```json
{
  "result": {
    "accessToken": "...",
    "accessTokenExpiresAt": "..."
  }
}
```

Правильный порядок:

1. Прочитать envelope.
2. Взять `Result`.
3. Из `Result` взять `AccessToken`.

Если попытаться десериализовать response напрямую как `ServiceTokenResponse`, token будет пустым.

## 9. Как token добавляется в gRPC request

В gRPC вместо обычных HTTP headers используется metadata.

ASP.NET Core gRPC понимает metadata key:

```text
Authorization
```

Пример:

```csharp
var headers = new Metadata
{
    { "Authorization", $"Bearer {accessToken}" }
};

await _grpcClient.CheckMediaAssetExistsAsync(
    request,
    headers: headers,
    cancellationToken: cancellationToken);
```

Для `FileService` это выглядит как обычный authenticated request:

1. `JwtBearer` достает token из `Authorization`.
2. Проверяет подпись, issuer, audience и lifetime.
3. Создает `ClaimsPrincipal`.
4. Authorization policy проверяет `service_permission`.

## 10. Как FileService защищает gRPC endpoint

В `FileService` policy отделена от user permissions:

```csharp
options.AddPolicy(FileAuthorizationPolicies.FILE_SERVICE_INTERNAL, policy =>
{
    policy.RequireAuthenticatedUser();
    policy.RequireClaim("service_permission", "file-service.internal");
});
```

gRPC endpoint:

```csharp
app.MapGrpcService<FileInternalGrpcService>()
    .RequireAuthorization(FileAuthorizationPolicies.FILE_SERVICE_INTERNAL);
```

Это означает:

- без JWT endpoint не вызывается;
- обычный user JWT с `files.read` не подходит;
- нужен service JWT с `service_permission=file-service.internal`;
- проверка находится на границе `FileService`, а не внутри business handler.

## 11. Почему token кэшируется

Если перед каждым gRPC-вызовом заново ходить в AuthService, получится лишняя цепочка:

```text
DirectoryService -> AuthService -> FileService
DirectoryService -> AuthService -> FileService
DirectoryService -> AuthService -> FileService
```

Минусы:

- выше latency (задержка ответа);
- лишняя нагрузка на AuthService;
- больше точек отказа;
- сложнее читать traces (трассировки запросов).

Поэтому token provider кэширует token до expiration (истечения срока действия).

Перед истечением token обновляется заранее с небольшим запасом:

```text
refresh skew = 30 seconds
```

Это снижает риск отправить в `FileService` token, который истек прямо во время network call.

## 12. Зачем нужен SemaphoreSlim

Если много запросов одновременно увидят, что token отсутствует или почти истек, без синхронизации каждый запрос может пойти в AuthService за новым token.

`SemaphoreSlim` делает обновление последовательным:

```text
Request A получает lock и обновляет token
Request B ждет
Request C ждет
Request A сохранил token
Request B/C используют уже обновленный token
```

Это не business-lock (бизнес-блокировка), а техническая защита от лишних одновременных запросов в AuthService.

## 13. Какие ошибки возможны

| Ситуация | Где падает | Ожидаемый смысл |
| --- | --- | --- |
| `ClientId` или `ClientSecret` неверный | AuthService `/api/auth/service-token` | DirectoryService не может получить service token. |
| AuthService недоступен | `AuthServiceTokenProvider` | Internal call не может быть авторизован. |
| Token истек или неверно подписан | FileService JwtBearer | `401 Unauthorized`. |
| Token валиден, но нет `service_permission` | FileService authorization policy | `403 Forbidden`. |
| gRPC endpoint недоступен | gRPC client | `RpcException`, adapter переводит в `Failure`. |

Business layer `DirectoryService` должен продолжать видеть `Result<T, Failure>`, а не transport exception.

## 14. Как добавить такой flow в новый service-to-service вызов

Порядок действий:

1. Решить, нужен ли sync-вызов.

   Если нужен ответ прямо сейчас, подходит gRPC или internal HTTP.

2. В provider service создать internal permission.

   Пример:

   ```text
   incident-service.internal
   ```

3. В AuthService добавить service client config с этим permission.

4. В contracts package provider service добавить `.proto`.

5. В consumer-side adapter добавить получение service token.

6. В gRPC metadata добавлять:

   ```text
   Authorization: Bearer <service-token>
   ```

7. В provider service повесить `.RequireAuthorization(...)` на gRPC endpoint.

8. Добавить tests:

   - валидный service client получает token;
   - неверный secret не получает token;
   - endpoint принимает token с правильным `service_permission`;
   - endpoint отклоняет token без нужного `service_permission`.

## 15. Когда использовать RabbitMQ вместо gRPC

gRPC подходит, когда сервису нужен ответ прямо сейчас.

Пример:

```text
DirectoryService спрашивает FileService: существует ли media asset?
```

RabbitMQ подходит, когда сервис сообщает факт, а другие сервисы могут обработать его позже.

Пример:

```text
FileService публикует событие FileUploaded.
NotificationService позже отправляет уведомление.
DirectoryService позже обновляет projection.
```

Главное правило:

```text
Нужен ответ сейчас -> gRPC/internal HTTP.
Нужно сообщить факт и обработать позже -> RabbitMQ.
```

## 16. Что проверено

Docker smoke path был подтвержден:

```text
DirectoryService получил service token через AuthService.
DirectoryService вызвал FileService по gRPC:
file.v1.FileInternal/CheckMediaAssetExists.
FileService принял service JWT с service_permission=file-service.internal.
Отсутствующий videoId вернул ожидаемый business 404, не 500.
```
