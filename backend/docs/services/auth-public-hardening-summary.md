# AuthService Public Auth Hardening Summary

Дата среза: 2026-07-16.

## Коротко

Этот срез добавляет первый защитный слой вокруг публичных auth endpoints AuthService.

Главная идея: публичные точки входа нельзя оставлять без ограничения частоты запросов и без реакции на серию неверных паролей. При этом AuthService не должен раскрывать наружу, что именно произошло: пользователь не найден, пароль неверный, аккаунт временно заблокирован или пользователь неактивен.

## Какие проблемы закрывали

1. Перебор пароля.

   До изменения login просто проверял пароль через `CheckPasswordAsync`. Такой вызов не увеличивал `AccessFailedCount` и не выставлял `LockoutEnd`, поэтому встроенная временная блокировка ASP.NET Core Identity фактически не работала.

2. Массовые повторные запросы к публичным endpoints.

   `login`, `refresh`, `request-password-reset`, `reset-password` и `resend-invite` являются чувствительными публичными или около-публичными точками. Их нужно ограничивать по частоте, чтобы снизить риск перебора, лишней нагрузки и массовой отправки писем.

3. Сохранение безопасной публичной ошибки.

   Нельзя сообщать клиенту: "пароль неверный", "аккаунт временно заблокирован" или "такой email существует". Все такие случаи должны оставаться одинаковыми снаружи.

## Что изменилось

### Login lockout

Login теперь использует `SignInManager.CheckPasswordSignInAsync(..., lockoutOnFailure: true)`.

Это включает стандартный механизм ASP.NET Core Identity:

- `AccessFailedCount` увеличивается при неверном пароле;
- после 3 неверных попыток выставляется `LockoutEnd`;
- временная блокировка входа длится 15 минут;
- правильный пароль во время блокировки тоже не пускает;
- наружу возвращается та же safe-ошибка `credentials.is.invalid`.

Важно: это не деактивация аккаунта.

Не происходит:

- `IsActive` не меняется;
- роли и permissions не меняются;
- refresh tokens не отзываются;
- пользовательские sessions не завершаются.

Это защита от подбора пароля, а не реакция на доказанный взлом.

### Rate limiting

Добавлен rate limiting, то есть ограничение частоты запросов, для public auth endpoints:

- `POST /api/auth/login`;
- `POST /api/auth/refresh`;
- `POST /api/auth/request-password-reset`;
- `POST /api/auth/reset-password`;
- `POST /api/users/{userId}/resend-invite`.

Лимиты заданы через `PublicAuthRateLimits`.

Текущие значения:

| Endpoint group | Лимит |
| --- | ---: |
| Login | 10 запросов за 60 секунд |
| Refresh | 30 запросов за 60 секунд |
| Password reset | 3 запроса за 60 секунд |
| Invite resend | 10 запросов за 60 секунд |

Ограничение работает по IP-адресу. Это выбранный MVP-вариант, потому что часть auth flows анонимная и у запроса еще может не быть user identity.

При превышении лимита endpoint возвращает:

```text
429 Too Many Requests
```

## Новый login flow

```text
Client -> AuthService: email + password
AuthService -> Identity: найти пользователя по email
AuthService: если user отсутствует или inactive -> credentials.is.invalid
AuthService -> Identity: включить lockout для старого user, если он был выключен
AuthService -> Identity: проверить password с lockoutOnFailure=true

Если пароль неверный:
  Identity увеличивает AccessFailedCount
  после 3-й ошибки выставляет LockoutEnd
  AuthService возвращает credentials.is.invalid

Если пользователь временно заблокирован:
  AuthService возвращает credentials.is.invalid

Если пароль верный и блокировки нет:
  AuthService выпускает access token + refresh token
  сохраняет refresh token hash
```

## Почему не отзываем refresh tokens при lockout

Три неверных пароля не доказывают, что аккаунт взломан. Это может быть забытый пароль, опечатка, автозаполнение старого пароля или чужая попытка угадать пароль.

Поэтому lockout блокирует только новый login. Уже существующие sessions не трогаются.

Refresh tokens отзываются в более сильных сценариях:

- password reset completed;
- admin deactivate user;
- admin revoke sessions;
- user revoke all sessions;
- refresh token reuse detection.

## Что это дало

1. Login получил реальную временную блокировку после 3 неверных паролей.

2. Public auth endpoints получили общий слой ограничения частоты запросов.

3. Security-safe ошибки сохранились: клиент не узнает, существует ли email, заблокирован ли user или просто неверный пароль.

4. Поведение стало конфигурируемым: лимиты можно менять через `PublicAuthRateLimits`.

5. Integration tests подтверждают:

   - 3 неверных пароля включают временную блокировку входа;
   - правильный пароль во время блокировки не пускает;
   - превышение rate limit возвращает `429`;
   - существующие login/refresh/password reset/invite flows не сломались.

## Проверки

Последние проверки для среза:

```text
dotnet build AuthService.sln
dotnet test AuthService.sln
```

Результат:

```text
build: 0 warnings / 0 errors
unit tests: 6/6
integration tests: 100/100
```

## Что осталось следующим шагом

Следующий ближайший блок: outbox/retry для email delivery invite/password reset.

Зачем: сейчас email отправляется после commit. Для production-grade доставки лучше сохранять email job в transaction и отправлять ее background worker-ом с retry/backoff.
