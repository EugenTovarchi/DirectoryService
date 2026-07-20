# AuthService: transactional email outbox, Quartz и retry

## Задача

Invite и password reset создают secret token в PostgreSQL, а затем должны отправить ссылку через SMTP. Прямой SMTP-вызов после commit создаёт опасное окно: business transaction уже завершена, но приложение может упасть до отправки письма. Повторить HTTP request автоматически нельзя, потому что он создаёт новый token и меняет business state.

Transactional outbox решает это разделением двух обязанностей:

- HTTP handler атомарно сохраняет business change и намерение отправить письмо.
- Фоновый обработчик независимо доставляет сохранённое письмо и управляет retry.

## Компоненты

| Компонент | Ответственность |
|---|---|
| `EmailOutboxMessage` | Хранит delivery state и защищает переходы success/failure/discard. |
| `IEmailOutboxRepository` | Добавляет запись и резервирует готовые письма. |
| `EmailOutboxRepository` | Использует PostgreSQL и `FOR UPDATE SKIP LOCKED`. |
| `EmailOutboxProcessor` | Отправляет одну выбранную пачку и сохраняет результат каждой попытки. |
| `EmailOutboxDeliveryJob` | Quartz-задание, которое регулярно запускает processor. |
| `IInviteEmailSender` | Adapter для invite SMTP delivery. |
| `IPasswordResetEmailSender` | Adapter для password reset SMTP delivery. |
| `EmailOutboxOptions` | Интервал запуска, число писем, retry/backoff и время резерва. |

## Flow создания invite/reset письма

```text
HTTP request
    -> Handler validation и authorization
    -> BEGIN PostgreSQL transaction
    -> Identity user / invite или reset token / audit event
    -> EmailOutboxMessage с delivery link
    -> один SaveChangesAsync
    -> COMMIT
    -> HTTP response
```

Главная гарантия: business change и outbox message либо сохраняются вместе, либо вместе откатываются. Состояние «token существует, но система забыла, что письмо нужно отправить» больше не возникает.

Для `POST /api/users/invite` в одной transaction сохраняются:

- inactive `ApplicationUser`;
- role assignment;
- hash invite token в `user_invite_tokens`;
- `InviteCreated` audit event;
- invite `EmailOutboxMessage`.

Для `POST /api/users/{userId}/resend-invite` сохраняются:

- revoke старого pending invite token;
- hash нового invite token;
- `InviteResent` audit event;
- новый invite `EmailOutboxMessage`.

Для `POST /api/auth/request-password-reset` сохраняются:

- revoke старых pending reset tokens;
- hash нового reset token;
- `PasswordResetRequested` audit event;
- password reset `EmailOutboxMessage`.

## Flow Quartz delivery

```text
Quartz SimpleTrigger
    -> EmailOutboxDeliveryJob
    -> EmailOutboxProcessor.ProcessBatchAsync
    -> reserve ready rows in PostgreSQL
    -> choose invite/password-reset sender by message type
    -> SMTP
       -> success: DeliveredAt + clear DeliveryLink
       -> failure: AttemptCount + NextAttemptAt
       -> terminal failure/expiration: DiscardedAt + clear DeliveryLink
```

Quartz запускается через in-memory store. Это безопасно для текущего flow, потому что Quartz не владеет отдельными письмами: он только периодически напоминает проверить PostgreSQL. После restart используется `StartNow()`, и processor снова видит все незавершённые записи.

## Почему FileService использует persistent Quartz, а AuthService нет

| AuthService email outbox | FileService video processing |
|---|---|
| Quartz хранит только периодический trigger. | Quartz job представляет конкретную video processing задачу. |
| Конкретные письма и retry находятся в PostgreSQL outbox. | Job data, recovery и cluster schedule являются частью выполнения видео. |
| Потеря in-memory trigger во время остановки не теряет письмо. | Потеря job может потерять запуск конкретной обработки. |
| После restart достаточно снова просканировать outbox. | Нужен persistent Quartz store и clustering. |

Правило выбора: если scheduler только будит durable queue, in-memory schedule обычно достаточно. Если scheduler сам владеет индивидуальными business jobs и точным расписанием, нужен persistent store.

## Резервирование при нескольких копиях AuthService

В production может работать несколько replicas AuthService. Каждая запускает свой Quartz trigger. Без координации две replicas могли бы одновременно выбрать одно письмо.

Repository выполняет выборку внутри короткой transaction:

```sql
SELECT *
FROM email_outbox_messages
WHERE delivered_at IS NULL
  AND discarded_at IS NULL
  AND next_attempt_at <= @now
  AND (processing_started_at IS NULL OR processing_started_at <= @leaseExpiredBefore)
ORDER BY created_at
FOR UPDATE SKIP LOCKED
LIMIT @messagesPerRun;
```

`FOR UPDATE` временно блокирует выбранные строки. `SKIP LOCKED` заставляет другую replica не ждать блокировку, а перейти к следующим письмам. После выбора `ProcessingStartedAt` сохраняется, transaction быстро завершается, и SMTP выполняется уже без долгой DB lock.

Если процесс упал после резервирования, `ProcessingLeaseSeconds` определяет, когда другая replica сможет повторно выбрать запись. Такая доставка имеет семантику at-least-once: редкий duplicate возможен, если SMTP принял письмо, но процесс упал до сохранения `DeliveredAt`. Email provider и шаблоны должны быть готовы к этому.

## Retry и exponential backoff

После ошибки processor не делает немедленный бесконечный цикл. Он рассчитывает задержку:

```text
delay = min(InitialRetryDelaySeconds * 2 ^ completedAttempts,
            MaxRetryDelaySeconds)
```

При initial delay 30 секунд попытки планируются примерно через:

- 30 секунд;
- 60 секунд;
- 120 секунд;
- 240 секунд;
- далее не больше configured maximum.

Backoff нужен, чтобы временно недоступный SMTP provider не получал постоянный поток повторных запросов и AuthService не тратил ресурсы на tight loop.

## Domain validation переходов

Outbox entity не должна полагаться только на правильный порядок вызовов в processor. Она сама отклоняет некорректные переходы через `UnitResult<Error>`:

- `MarkDelivered` нельзя вызвать без предварительного `MarkProcessing`;
- delivered/discarded message нельзя обработать повторно;
- delivery/failure timestamp не может быть раньше начала обработки;
- `maxAttempts` должен быть положительным;
- `nextAttemptAt` должен быть позже времени ошибки;
- `DiscardExpired` разрешён только после `ExpiresAt`;
- повреждённый delivery payload завершается отдельным `DiscardInvalidPayload` и очищается.

Такой подход защищает invariants независимо от HTTP handler, Quartz или repository и делает ошибки переходов проверяемыми unit tests без exceptions.

## Что означает MessagesPerRun

`MessagesPerRun` ограничивает число outbox rows, которые один запуск processor резервирует из БД. Это не SMTP batch: каждое письмо отправляется отдельным вызовом adapter.

Если в outbox 35 готовых писем, а `MessagesPerRun = 10`, четыре последовательных запуска обработают 10, 10, 10 и 5 записей. Ограничение делает длительность одного job предсказуемой и не позволяет большой очереди занять все ресурсы процесса.

## Security model

Основные token tables по-прежнему безопасны для lookup:

- `user_invite_tokens` хранит только hash invite token;
- `password_reset_tokens` хранит только hash reset token.

Но outbox должен пережить restart и повторно построить полную ссылку, поэтому raw token временно находится внутри `delivery_link`. Это осознанный security trade-off transactional delivery.

Правила:

- не хранить raw token отдельным полем вне delivery payload;
- не писать link/token в application logs, audit events и error responses;
- очищать `DeliveryLink` после success, terminal failure и expiration;
- хранить в `LastFailureCode` только безопасный код, а не provider response с возможными sensitive данными;
- ограничить DB permissions на outbox table;
- использовать PostgreSQL/storage encryption at rest;
- перед production оценить application-level encryption delivery payload и безопасное хранение ключа.

## Поведение при сбоях

| Сбой | Результат |
|---|---|
| Ошибка до transaction commit | Business change и outbox вместе откатываются. |
| Падение после commit до Quartz запуска | Outbox остаётся в PostgreSQL и будет выбран после restart. |
| SMTP временно недоступен | Сохраняются `AttemptCount`, safe failure code и `NextAttemptAt`. |
| Процесс упал после резервирования | После истечения времени резерва запись подберёт следующий processor. |
| Token истёк до доставки | Запись получает `DiscardedAt`, link очищается. |
| Исчерпаны попытки | Запись получает `DiscardedAt`, link очищается. |
| SMTP принял письмо, процесс упал до DB update | Возможна повторная доставка; это стандартная граница at-least-once. |

## Как тестировать такой flow

Unit test entity:

1. Arrange: создать `EmailOutboxMessage`.
2. Act: вызвать один state transition (`MarkDelivered`, `RegisterFailure`, `DiscardExpired`).
3. Assert: проверить timestamps, attempt count и очистку payload.

Integration test:

1. Отключить Quartz timer в Testing configuration.
2. Выполнить HTTP endpoint или сохранить outbox message в Testcontainers PostgreSQL.
3. Явно вызвать `EmailOutboxProcessor.ProcessBatchAsync()`.
4. Использовать controllable fake sender вместо реального SMTP.
5. После первой настроенной ошибки проверить persisted retry state.
6. После backoff снова вызвать processor и проверить delivery state.

Почему timer отключается: тест должен управлять моментом выполнения и не зависеть от скорости CI. Проверяется тот же processor, который вызывает production Quartz job, поэтому business behavior не подменяется.

## Как перенести подход в другой сервис

1. Определить business transaction, после которой требуется внешний side effect.
2. Создать outbox entity с payload, created/next-attempt timestamps и terminal states.
3. Добавлять outbox record тем же DbContext и тем же transaction manager.
4. Вынести отправку во внешний adapter interface.
5. Сделать processor независимым от Quartz, чтобы его можно было тестировать напрямую.
6. Использовать scheduler только для запуска processor.
7. Для нескольких replicas добавить безопасное резервирование строк.
8. Явно определить at-least-once semantics и idempotency/duplicate policy.
9. Очищать sensitive payload, когда он больше не нужен.
10. Покрыть state transitions unit tests и полный delivery flow integration test.
