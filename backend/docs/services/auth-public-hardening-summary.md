# AuthService Public Auth Hardening Guide

Дата среза: 2026-07-16.

Этот документ - путеводитель по защите публичных auth endpoints на примере AuthService. Он объясняет не только что было сделано в текущем проекте, но и как повторить такой подход в новом сервисе, если защиты еще нет.

## 1. Что мы защищаем

Публичные auth endpoints - это точки, куда может прийти внешний клиент до полноценной авторизации или рядом с ней:

- `POST /api/auth/login`;
- `POST /api/auth/refresh`;
- `POST /api/auth/request-password-reset`;
- `POST /api/auth/reset-password`;
- `POST /api/users/{userId}/resend-invite`.

Для таких endpoints важны две разные защиты:

1. Rate limiting (ограничение частоты запросов).

   Это защита endpoint-а. Она отвечает на вопрос: "не слишком ли часто этот IP/client вызывает endpoint?"

2. Login lockout (временная блокировка входа).

   Это защита конкретного user account. Она отвечает на вопрос: "не было ли слишком много неверных паролей для этого пользователя?"

Эти механизмы нельзя смешивать. Rate limiting может сработать даже для неизвестного email. Login lockout работает только тогда, когда пользователь найден.

## 2. Какие проблемы решаются

### Перебор пароля

Если login просто вызывает:

```csharp
bool passwordIsValid = await _userManager.CheckPasswordAsync(user, password);
```

то ASP.NET Core Identity только проверяет пароль. Такой вызов не увеличивает `AccessFailedCount` и не выставляет `LockoutEnd`. Значит временная блокировка входа фактически не работает.

### Массовые повторные запросы

Даже если login lockout включен, злоумышленник или ошибочный клиент может постоянно дергать:

- login;
- refresh;
- password reset;
- invite resend.

Это создает лишнюю нагрузку, может приводить к массовой отправке email и усложняет диагностику.

### Утечка информации через ошибки

AuthService не должен отвечать разными сообщениями:

```text
user.not.found
password.is.invalid
user.is.locked
```

Снаружи все эти случаи должны выглядеть одинаково:

```text
credentials.is.invalid
```

Так клиент и потенциальный атакующий не понимают, существует ли email, неверный ли пароль или пользователь временно заблокирован.

## 3. Что реализовано в AuthService

### Login lockout

Login переведен на:

```csharp
Microsoft.AspNetCore.Identity.SignInResult signInResult =
    await _signInManager.CheckPasswordSignInAsync(
        user,
        command.Request.Password,
        lockoutOnFailure: true);

if (!signInResult.Succeeded)
    return AuthFailures.InvalidCredentials();
```

Identity настроен так:

```csharp
options.Lockout.AllowedForNewUsers = true;
options.Lockout.MaxFailedAccessAttempts = 3;
options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
```

Поведение:

- 1-й неверный пароль увеличивает `AccessFailedCount`;
- 2-й неверный пароль снова увеличивает `AccessFailedCount`;
- 3-й неверный пароль выставляет `LockoutEnd`;
- до `LockoutEnd` правильный пароль тоже не пускает;
- наружу возвращается та же ошибка `credentials.is.invalid`.

Важно: это не деактивация аккаунта.

Не происходит:

- `IsActive` не меняется;
- роли и permissions не меняются;
- refresh tokens не отзываются;
- существующие sessions не завершаются.

### Rate limiting

Добавлен rate limiting для public auth endpoints:

| Endpoint group | Лимит |
| --- | ---: |
| Login | 10 запросов за 60 секунд |
| Refresh | 30 запросов за 60 секунд |
| Password reset | 3 запроса за 60 секунд |
| Invite resend | 10 запросов за 60 секунд |

Ограничение считается по IP-адресу. Это MVP-вариант, потому что часть auth flows анонимная и у запроса еще может не быть user identity.

При превышении лимита endpoint возвращает:

```text
429 Too Many Requests
```

## 4. Как настроить лимиты

Создаем options-класс:

```csharp
public sealed class PublicAuthRateLimitOptions
{
    public const string SECTION_NAME = "PublicAuthRateLimits";

    public bool Enabled { get; set; } = true;
    public int WindowSeconds { get; set; } = 60;
    public int LoginPermitLimit { get; set; } = 10;
    public int RefreshPermitLimit { get; set; } = 30;
    public int PasswordResetPermitLimit { get; set; } = 3;
    public int InviteResendPermitLimit { get; set; } = 10;
}
```

Добавляем секцию в `appsettings.json`:

```json
"PublicAuthRateLimits": {
  "Enabled": true,
  "WindowSeconds": 60,
  "LoginPermitLimit": 10,
  "RefreshPermitLimit": 30,
  "PasswordResetPermitLimit": 3,
  "InviteResendPermitLimit": 10
}
```

В testing-конфигурации можно поставить высокие значения, чтобы весь integration suite не упирался в `429`. Для отдельного rate-limit теста лимит лучше переопределять точечно.

## 5. Как зарегистрировать rate limiter

Упрощенный пример:

```csharp
services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("public-auth-login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 10,
                QueueLimit = 0,
                Window = TimeSpan.FromSeconds(60),
            }));
});
```

В pipeline:

```csharp
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
```

В текущем AuthService реализация гибче: policy читает `IOptionsMonitor<PublicAuthRateLimitOptions>` во время запроса, чтобы test overrides (переопределения в тестах) и config reload (перезагрузка конфигурации) влияли на новые окна.

## 6. Как повесить лимит на endpoint

Endpoint сам не считает запросы. Он только объявляет, какая named policy (именованное правило) к нему применяется.

```csharp
app.MapPost(
    "/api/auth/login",
    async Task<EndpointResult<TokenResponse>> (
        [FromBody] LoginRequest request,
        HttpContext httpContext,
        [FromServices] LoginHandler handler,
        CancellationToken cancellationToken) =>
    {
        string? ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        string? userAgent = httpContext.Request.Headers.UserAgent.FirstOrDefault();
        LoginCommand command = new(request, ipAddress, userAgent);

        return await handler.Handle(command, cancellationToken);
    })
    .RequireRateLimiting(PublicAuthRateLimitPolicies.LOGIN);
```

Так endpoint остается читаемым:

- HTTP route и binding описаны в mapping;
- business flow остается в handler;
- rate-limit policy видна на endpoint boundary.

## 7. Как включить login lockout в новом проекте

### Шаг 1. Добавить SignInManager

Если проект использует `AddIdentityCore`, нужно явно добавить SignInManager:

```csharp
services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;

        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 3;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddRoles<ApplicationRole>()
    .AddSignInManager()
    .AddEntityFrameworkStores<AuthServiceDbContext>()
    .AddDefaultTokenProviders();
```

### Шаг 2. Проверить, что lockout columns есть в БД

Для ASP.NET Core Identity обычно нужны поля:

- `AccessFailedCount`;
- `LockoutEnabled`;
- `LockoutEnd`.

В текущем AuthService они уже были в `identity_users`, поэтому новая migration не понадобилась.

### Шаг 3. Заменить проверку пароля

Было:

```csharp
bool passwordIsValid = await _userManager.CheckPasswordAsync(user, password);
if (!passwordIsValid)
    return AuthFailures.InvalidCredentials();
```

Стало:

```csharp
SignInResult result = await _signInManager.CheckPasswordSignInAsync(
    user,
    password,
    lockoutOnFailure: true);

if (!result.Succeeded)
    return AuthFailures.InvalidCredentials();
```

### Шаг 4. Сохранить safe public error

Не нужно проверять `result.IsLockedOut` и возвращать отдельную ошибку наружу.

Так делать не надо:

```csharp
if (result.IsLockedOut)
    return AuthFailures.UserIsLocked();
```

Правильно для public login:

```csharp
if (!result.Succeeded)
    return AuthFailures.InvalidCredentials();
```

## 8. Новый login flow

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

## 9. Почему не отзываем refresh tokens при lockout

Три неверных пароля не доказывают, что аккаунт взломан. Это может быть забытый пароль, опечатка, автозаполнение старого пароля или чужая попытка угадать пароль.

Поэтому lockout блокирует только новый login. Уже существующие sessions не трогаются.

Refresh tokens отзываются в более сильных сценариях:

- password reset completed;
- admin deactivate user;
- admin revoke sessions;
- user revoke all sessions;
- refresh token reuse detection.

## 10. Какие тесты нужны

Минимальный набор:

1. Valid login возвращает access token и refresh token.

2. Неверный пароль возвращает `BadRequest` и safe error.

3. Три неверных пароля выставляют `LockoutEnd`.

4. Правильный пароль во время lockout не пускает.

5. Превышение rate limit возвращает `429 Too Many Requests`.

6. Остальные auth flows не ломаются из-за слишком низких лимитов в integration tests.

Пример rate-limit теста:

```csharp
services.PostConfigure<PublicAuthRateLimitOptions>(options =>
{
    options.WindowSeconds = 60;
    options.LoginPermitLimit = 1;
});

HttpResponseMessage first = await client.PostAsJsonAsync("/api/auth/login", request);
HttpResponseMessage second = await client.PostAsJsonAsync("/api/auth/login", request);

first.StatusCode.Should().Be(HttpStatusCode.BadRequest);
second.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
```

## 11. Что это дало

1. Login получил реальную временную блокировку после 3 неверных паролей.

2. Public auth endpoints получили общий слой ограничения частоты запросов.

3. Security-safe ошибки сохранились: клиент не узнает, существует ли email, заблокирован ли user или просто неверный пароль.

4. Поведение стало конфигурируемым: лимиты можно менять через `PublicAuthRateLimits`.

5. Integration tests подтверждают:

   - 3 неверных пароля включают временную блокировку входа;
   - правильный пароль во время блокировки не пускает;
   - превышение rate limit возвращает `429`;
   - существующие login/refresh/password reset/invite flows не сломались.

## 12. Проверки

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

## 13. Что осталось следующим шагом

Следующий ближайший блок: outbox/retry для email delivery invite/password reset.

Зачем: сейчас email отправляется после commit. Для production-grade доставки лучше сохранять email job в transaction и отправлять ее background worker-ом с retry/backoff.
