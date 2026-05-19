# AuthService

См. также [../architecture/overview.md](../architecture/overview.md), [directory-service.md](directory-service.md) и [file-service.md](file-service.md).

## Назначение

AuthService отвечает за identity и жизненный цикл токенов в B2B backend для видеохостинга и видеомониторинга:

- пользователи;
- учетные данные и password hashing через ASP.NET Core Identity;
- роли;
- permissions;
- выдача access token;
- rotation и отзыв refresh token;
- минимальный company/tenant context.

AuthService не должен владеть бизнес-правилами видео, камер, файлов, подразделений, локаций или иерархии доступа.

AuthService является security-critical сервисом. Любое изменение users, credentials, roles, permissions, token issuing, refresh token storage, invite flow или session revocation нужно делать маленькими проверяемыми шагами, с тестами и без логирования секретов. Ошибка здесь может привести не просто к багу, а к обходу авторизации, утечке учетных данных или доступу пользователя к чужой компании.

`Program.cs` должен оставаться коротким composition root. Настройку JWT, options validation, authentication/authorization policies, Identity wiring и другие security-sensitive registrations выносим в focused extension methods или отдельные configuration classes. Это упрощает review и снижает риск случайно смешать startup wiring с бизнес-логикой.

## Контекст Продукта

Проект строится как production-like B2B видеохостинг и система видеомониторинга. Компании могут подключать камеры, загружать медиа, обрабатывать видео и давать сотрудникам доступ только к своей части структуры компании.

Текущие границы сервисов:

- `AuthService`: кто пользователь и какие coarse-grained permissions у него есть.
- `DirectoryService`: структура компании, подразделения, локации, иерархия камер/объектов и access scopes.
- `FileService`: media assets, загрузка файлов, обработка видео, HLS, previews и storage.
- `SharedService`: service-neutral framework-код, распространяемый как NuGet packages.

## Границы Ответственности

AuthService отвечает на identity-вопросы:

- Зарегистрирован ли пользователь и активен ли он?
- Корректные ли учетные данные передал пользователь?
- Какие роли и permissions можно положить в access token?
- Валиден ли refresh token, истек ли он, отозван ли он или использован повторно?
- В каком company context сейчас работает пользователь?

AuthService не отвечает на детальные бизнес-вопросы доступа:

- Может ли пользователь смотреть камеру `X`?
- Может ли пользователь получить доступ к файлам внутри department subtree `Y`?
- Может ли пользователь удалить конкретный video asset?
- Какие подразделения видны пользователю?

Такие проверки принадлежат сервису-владельцу данных: обычно `DirectoryService` или `FileService`.

## Начальные Архитектурные Решения

- Используем ASP.NET Core Identity с `Guid` keys.
- Используем PostgreSQL для Identity и refresh token storage.
- Начинаем с Bearer JWT в `Authorization` header.
- Browser `HttpOnly` cookie auth оставляем как отдельную тему на будущее.
- Access token короткоживущий и не хранится на сервере.
- Refresh token долгоживущий, хранится на сервере как hash и ротируется при каждом refresh.
- FileService и DirectoryService валидируют access JWT локально: issuer, audience, signing key, lifetime и claims.
- Не кладем большие access trees и high-cardinality данные в JWT claims.
- Не храним JWT secrets в committed appsettings files.
- Не логируем access tokens и refresh tokens.

## Authentication Flow

Login flow:

```text
Client -> AuthService: email + password
AuthService -> PostgreSQL/Identity: проверяет credentials
AuthService -> PostgreSQL: создает refresh token record
AuthService -> Client: access token + refresh token
Client -> FileService/DirectoryService: Authorization: Bearer <access_token>
FileService/DirectoryService: локально валидируют JWT и применяют policies/business checks
```

Login implementation rules:

- `POST /api/auth/login` проверяет credentials через ASP.NET Core Identity `UserManager`.
- При неверном email, неверном password или inactive user возвращаем одинаковую ошибку `credentials.is.invalid`, чтобы не раскрывать существование учетной записи.
- `accessToken` является JWT и содержит короткоживущие claims: `sub`, `email`, `name`, `company_id`, `role`, `permission`, `jti`.
- `refreshToken` не является JWT. Это случайная секретная строка, которая возвращается клиенту только в raw-виде.
- В таблицу `refresh_tokens` сохраняется только hash refresh token, а не raw token.
- Login сохраняет session metadata: IP address, если он доступен в HTTP context, и raw `UserAgent`.
- `TokenResponse` явно возвращает `AccessTokenExpiresAt` и `RefreshTokenExpiresAt`, чтобы клиент не путал сроки жизни разных tokens.

Refresh flow:

```text
Client -> AuthService: expired/near-expired access token + refresh token
AuthService -> PostgreSQL: ищет refresh token hash
AuthService: отклоняет запрос, если token expired, revoked или reused
AuthService -> PostgreSQL: отзывает старый refresh token и создает replacement
AuthService -> Client: new access token + new refresh token
```

Refresh implementation rules:

- `POST /api/auth/refresh` accepts only raw `refreshToken`; expired access token is not required for MVP refresh.
- AuthService hashes the raw refresh token and looks up `refresh_tokens.token_hash`; raw refresh tokens are never stored or logged.
- Valid active refresh token rotation creates a replacement refresh token, marks the previous token as used/revoked, and stores `replaced_by_token_id`.
- If a revoked/replaced refresh token is submitted again, AuthService treats it as reuse and revokes all currently active refresh token sessions for that user.
- Unknown, expired, revoked, reused, and inactive-user refresh attempts return the same public auth failure shape to avoid leaking token/session state.
- The response shape remains `TokenResponse`: new access JWT, new raw refresh token, and both expiration timestamps.

Auth failure implementation rule:

- Login, refresh and authenticated self-profile flows use AuthService-local `AuthFailures` helpers for public auth failure shapes.
- Missing user, wrong password, inactive user, invalid refresh token, reused refresh token and inactive authenticated user must not reveal which exact condition happened.
- SharedService is not extended for these helpers while the semantics are AuthService-specific.

Logout/revoke flow:

```text
Client -> AuthService: refresh token
AuthService -> PostgreSQL: отзывает refresh token
Client: удаляет local token state
```

Logout implementation rules:

- `POST /api/auth/logout` accepts raw `refreshToken` and revokes the matching refresh token session when it is still active.
- Logout is idempotent for unknown, expired, or already revoked refresh tokens and returns `200 OK` without exposing token/session state.
- Logout revokes one refresh token session; revoke-all and session management endpoints are separate flows.
- The endpoint is named for the user-facing scenario (`LogoutEndpoint`), while the domain operation remains refresh token revocation.

Revoke all sessions implementation rules:

- `POST /api/auth/revoke-all-sessions` is authenticated and reads the current user id from the access token `sub` claim.
- The request has no body; clients cannot pass an arbitrary `userId`.
- The endpoint revokes every active refresh token session for the current user, including the session that issued the access token used for the request.
- Other users' refresh token sessions are not affected.
- The endpoint returns `200 OK` when the command succeeds. After this call the user must login again to get a usable refresh token.

## Access Token Claims

Начальные claims для access token:

- `sub`: user id;
- `email`: user email;
- `name`: display name, если есть;
- `company_id`: текущий company context;
- `role`: роли пользователя;
- `permission`: итоговые permissions пользователя;
- `jti`: access token id;
- `iss`: issuer;
- `aud`: audience;
- `exp`: expiration.

Claims должны оставаться небольшими и стабильными. Не кладем в token полные деревья подразделений, списки камер, file ids или другие high-cardinality business data.

## JWT Configuration

JWT-настройки подключаются через `IOptions<JwtOptions>` и extension methods, а не напрямую в `Program.cs`.

Поля:

- `Issuer`: кто выпустил token, например `24eye.auth`.
- `Audience`: для кого token предназначен, например `24eye.backend`.
- `SigningKey`: секрет подписи JWT для текущего symmetric signing варианта.
- `AccessTokenLifetimeMinutes`: срок жизни access token.
- `RefreshTokenLifetimeDays`: срок жизни refresh token session.

`SigningKey` не храним в committed appsettings. Для локального запуска используем User Secrets, для Docker local-dev используем `AuthService.Development.env`, для production нужен secret manager или CI/CD secrets.

Текущий MVP использует symmetric signing: AuthService подписывает JWT и downstream-сервисы проверяют подпись тем же секретом. Следующий security-hardening шаг - перейти на private/public key signing, где AuthService хранит private key, а FileService/DirectoryService получают только public key для проверки подписи.

## Roles

Начальные роли:

- `SystemAdmin`: platform-level administration.
- `CompanyAdmin`: company-level user и access administration.
- `Operator`: работа с камерами, видео, incidents и назначенными company objects.
- `Technician`: обслуживание камер, устройств и технических workflows.
- `Viewer`: read-only доступ к разрешенным company objects и media.

Потенциальные роли на будущее:

- `Manager`: operational management workflows.

## Permissions

Начинаем с небольшого набора permissions:

- `users.manage`
- `directory.read`
- `directory.manage`
- `files.read`
- `files.upload`
- `videos.read`
- `videos.upload`

Потенциальные permissions на будущее:

- `companies.read`
- `companies.manage`
- `cameras.read`
- `cameras.manage`
- `videos.delete`
- `files.delete`
- `incidents.read`
- `incidents.manage`
- `observability.read`

Permissions используются для policy-based authorization. Roles удобны, чтобы назначать группы permissions. Endpoint checks по возможности должны опираться на policies/permissions, а не на hard-coded role checks.

## Identity Seed

Стартовые roles, permissions и связи role-permission создаются runtime seeder-ом после EF migrations. Не используем `HasData`, потому что Identity roles лучше создавать через `RoleManager`: он корректно применяет Identity-нормализацию и остается совместимым с будущим seed первого `SystemAdmin`.

Seed должен быть идемпотентным: повторный запуск приложения или migrator не должен создавать дубликаты.

Начальная матрица permissions:

- `SystemAdmin`: все стартовые permissions.
- `CompanyAdmin`: `users.manage`, `directory.read`, `directory.manage`, `files.read`, `files.upload`, `videos.read`, `videos.upload`.
- `Operator`: `directory.read`, `files.read`, `files.upload`, `videos.read`, `videos.upload`.
- `Technician`: `directory.read`, `files.read`, `videos.read`, `videos.upload`.
- `Viewer`: `directory.read`, `files.read`, `videos.read`.

Эта матрица является MVP-стартом. Ее можно сужать по мере появления точных endpoints и product rules, но расширение прав должно проходить как security-sensitive изменение.

## First Protected Flow Permissions

Первый integration slice должен проверить auth не абстрактно, а через реальные сценарии FileService и DirectoryService.

FileService permissions:

- `files.read`: читать media metadata и download metadata.
- `files.upload`: запускать upload workflows.
- `videos.read`: читать video metadata/HLS-related metadata.
- `videos.upload`: запускать video upload workflow.

DirectoryService permissions:

- `directory.read`: читать company structure, departments, locations.
- `directory.manage`: создавать и изменять company structure.

AuthService permissions:

- `users.manage`: приглашать пользователей, менять роли, активировать/деактивировать пользователей компании.

Остальные permissions оставляем для следующих slices, когда появятся соответствующие workflows.

## Planned Endpoints

Public endpoints:

- `POST /api/auth/login`
- `POST /api/auth/refresh`
- `POST /api/auth/logout`

Authenticated endpoints:

- `GET /api/auth/me`
- `POST /api/auth/revoke-session`
- `GET /api/auth/sessions`
- `POST /api/auth/revoke-all-sessions`

Administrative endpoints:

- `GET /api/users`
- `GET /api/users/{userId}`
- `POST /api/users`
- `POST /api/users/invite`
- `PATCH /api/users/{userId}/roles`
- `PATCH /api/users/{userId}/status`
- `GET /api/users/{userId}/sessions`
- `POST /api/users/{userId}/revoke-sessions`

Self-registration для MVP не используем. Пользователи появляются через invite или создаются администратором компании.
Administrative endpoints можно добавить после того, как заработает core token lifecycle.

## Current User Endpoint

`GET /api/auth/me` - authenticated endpoint для текущего пользователя. Endpoint читает user id из `sub` claim через `ClaimsPrincipalExtensions.GetUserId()`, требует `.RequireAuthorization()` и не принимает `userId` от клиента.

Response возвращает:

- `id`;
- `email`;
- `username`;
- `displayName`;
- `currentCompanyId` как MVP company context;
- `roles`;
- итоговые `permissions`.

Handler перечитывает текущего Identity user через `UserManager<ApplicationUser>`, роли через `UserManager.GetRolesAsync(...)`, permissions через `IRolePermissionReader`. Это сохраняет thin endpoint и не доверяет клиентскому input для выбора пользователя.

Основной клиентский сценарий: после login или перезагрузки приложения frontend вызывает `/api/auth/me`, проверяет, что access token еще валиден, и получает стабильный API contract для app shell, navigation, user menu, permission gates и текущего company context. Frontend не должен зависеть от внутреннего состава JWT claims как от основного источника UI-state.

## Черновая Data Model

Identity tables предоставляет ASP.NET Core Identity. Мы кастомизируем их под `Guid` keys.

Планируемые custom entities:

- `ApplicationUser`
  - `Id`
  - `Email`
  - `UserName`
  - `DisplayName`
  - `IsActive`
  - `CurrentCompanyId`
  - `CreatedAt`
- `ApplicationRole`
  - `Id`
  - `Name`
  - `Description`
- `Permission`
  - `Id`
  - `Code`
  - `Description`
- `RolePermission`
  - `RoleId`
  - `PermissionId`
- `RefreshToken`
  - `Id`
  - `UserId`
  - `TokenHash`
  - `CreatedAt`
  - `ExpiresAt`
  - `RevokedAt`
  - `ReplacedByTokenId`
  - `CreatedByIp`
  - `RevokedByIp`

`CompanyId` здесь является ссылкой на company context. Полная структура компании остается ответственностью `DirectoryService`.

`UserName` в ASP.NET Core Identity остается строковым persistence/auth field, но входные значения валидируем через локальный AuthService value object `Username`. Это сохраняет совместимость с Identity и не дает разнести правила нормализации по handlers/endpoints.

`DisplayName` является optional UI/profile field, а не юридическим ФИО. Для него используем локальный AuthService value object, чтобы валидировать длину и нормализацию пробелов рядом с identity domain. Не используем `DirectoryService.Contracts.ValueObjects.Name`, потому что это контракт DirectoryService для directory-domain имен, и не выносим `DisplayName` в `SharedService`, пока нет service-neutral сценария повторного использования.

## Company Membership Decision

Для MVP храним `CurrentCompanyId` прямо в `ApplicationUser`.

Почему так:

- MVP предполагает, что один пользователь принадлежит одной company.
- Это проще для первого production-like slice и снижает количество таблиц/проверок.
- Полная company structure все равно остается в `DirectoryService`.
- Multi-company membership нужен не сейчас, а когда появятся сценарии холдингов, подрядчиков или операторов нескольких клиентов.

Post-MVP upgrade path:

- добавить `UserCompanyMembership`;
- хранить `CompanyId`, `UserId`, company-level roles/status;
- поддержать выбор active company context;
- пересмотреть claims и token issuing flow.

## Contracts Decision

В репозитории уже есть `AuthService.Contracts`. Используем этот проект для DTO/контрактов, которые могут потреблять другие сервисы или внешние клиенты.

Правила:

- Не создавать новый contracts package на MVP.
- Public DTO держать в `AuthService.Contracts`.
- Внутренние command/query models могут жить внутри `AuthService.Core` или `AuthService.Web`, если не являются внешним контрактом.
- После публикации DTO в external API менять их аккуратно и обратно совместимо.

## MVP Product Decisions

Для MVP считаем продукт закрытым B2B-сервисом. Обычный пользователь не может сам зарегистрироваться и начать смотреть видео. Компания сначала становится клиентом продукта, после этого ее администраторы или сотрудники получают доступ.

Решения:

- Один пользователь принадлежит одной company на MVP stage.
- Multi-company membership оставляем на будущее, если появятся сценарии подрядчиков, холдингов или операторов, работающих с несколькими клиентами.
- `CompanyId` назначается при invite/admin-created user flow, а не вводится пользователем при public registration.
- Public registration в MVP не делаем.
- Основной сценарий: `CompanyAdmin` приглашает пользователя в свою company.
- Системный onboarding-сценарий: `SystemAdmin` создает company и первого `CompanyAdmin`.
- Invite token lifetime: 24 часа.
- Роль `Technician` поддерживаем сразу, потому что для видеомониторинга и обслуживания камер это естественная product role.
- Resend invite создает новый invite token, а старый pending invite переводит в `Revoked` или `Expired`.
- Email domain restriction в MVP не включаем.
- Минимальную audit history для invite/user lifecycle ведем сразу.
- Active sessions endpoint поддерживаем сразу.
- Revoke all sessions поддерживаем сразу для пользователя и для администратора.
- Invite link использует 24Eye branding.
- `Revoke all sessions` по умолчанию отзывает все refresh token sessions, включая текущую.
- Для отображения sessions нормализуем `UserAgent` до browser, OS и device type.
- При `Revoke all sessions` отправляем security email notification пользователю.
- Rate limiting важен для invite resend, security notifications и auth-sensitive endpoints, но не входит в первый MVP implementation slice.
- Для `revoke all sessions` на MVP достаточно authenticated request; повторный ввод пароля оставляем на future step-up auth.
- Отдельную device fingerprint таблицу на MVP не делаем; используем refresh token session metadata.
- Approximate location по IP в session list не показываем в MVP.

Почему так:

- Продукт коммерческий и предназначен для магазинов, торговых центров, охраны, диспетчеров и технических сотрудников.
- Пользователь без компании не имеет полезного сценария внутри системы.
- Public registration усложняет безопасность, tenant isolation и модерацию, но не дает пользы для B2B MVP.
- Invite/admin-created flow лучше соответствует модели, где доступ выдается сотрудникам конкретной организации.
- Управляемый onboarding через `SystemAdmin` проще и безопаснее для MVP, чем публичный company signup: мы явно контролируем создание клиента, первого администратора и начальные права.
- Жесткий email domain restriction плохо подходит для парковок, ЖК, лифтов, охранных организаций и подрядчиков: у участников процесса может не быть корпоративной почты клиента.
- Audit history нужна сразу, потому что invite и изменение ролей являются security-sensitive действиями.
- Active sessions и revoke all sessions полезны для security UX: пользователь или администратор может завершить сессии после потери устройства, увольнения сотрудника или подозрения на компрометацию.
- При массовом отзыве sessions безопаснее завершить и текущую session: если действие выполняется из-за компрометации, нельзя считать текущий refresh token доверенным.
- Security notification по email помогает пользователю заметить подозрительное действие и соответствует привычному UX identity-продуктов.
- Rate limiting защищает от email flooding, brute force и abuse сценариев.
- Для первого MVP slice rate limiting можно отложить, если мы не открываем сервис наружу и работаем в local/dev окружении. Перед публичным stage это надо добавить.
- Повторный ввод пароля перед `revoke all sessions` полезен для high-risk действий, но на MVP усложнит UX и реализацию; позже это можно заменить step-up auth/MFA.
- Device fingerprint может давать ложную точность и добавляет privacy/complexity; для MVP достаточно IP, raw `UserAgent`, normalized device info и refresh token session id.
- Approximate location по IP может быть полезна в session UI, но она неточная и может путать пользователей; для MVP достаточно IP/UserAgent/device metadata.

## Invite Link

Invite link должен вести на frontend/onboarding page, где пользователь принимает приглашение и задает пароль.

Local dev:

```text
http://localhost:3000/24eye/invite?token=<invite_token>
```

Production draft:

```text
https://app.24eye.example/invite?token=<invite_token>
```

Rules:

- URL хранится в configuration, а не hard-coded в коде.
- Backend генерирует cryptographically secure invite token.
- В БД хранится только hash invite token.
- В email отправляется raw invite token только как часть invite link.
- В logs/audit raw invite token не пишется.
- Если invite истек, пользователь должен запросить новое приглашение у администратора.

## Invite Lifecycle

Invite statuses:

- `Pending`: invite создан и еще не использован.
- `Accepted`: пользователь принял invite и активировал доступ.
- `Revoked`: invite отозван администратором или заменен новым invite.
- `Expired`: invite истек.

Rules:

- Invite token действует 24 часа.
- Raw invite token не храним в БД и не логируем.
- В БД храним только hash invite token.
- Resend invite не продлевает старый token.
- Resend invite отзывает старый pending invite и создает новый pending invite на 24 часа.
- Invite может отправляться на любой email, если это разрешено `CompanyAdmin` или `SystemAdmin`.

Future option:

- `CompanyAllowedDomain` / verified domains можно добавить позже как enterprise-настройку для клиентов, которым нужна строгая доменная политика.

## Auth Audit Events

Для MVP ведем минимальную audit history внутри AuthService.

Initial events:

- `InviteCreated`
- `InviteResent`
- `InviteAccepted`
- `InviteRevoked`
- `InviteExpired`
- `UserActivated`
- `UserDeactivated`
- `UserRoleChanged`
- `SessionRevoked`
- `AllSessionsRevoked`

Минимальные поля audit event:

- `Id`
- `CompanyId`
- `UserId`
- `Email`
- `Action`
- `ActorUserId`
- `CreatedAt`
- `IpAddress`
- `UserAgent`
- `MetadataJson`

Audit rules:

- Не логировать raw invite tokens.
- Не логировать access tokens.
- Не логировать refresh tokens.
- Не складывать secrets в `MetadataJson`.
- Email можно хранить как часть auth/audit домена, но не использовать audit logs для публичных API responses.

## Session Management

Для MVP session соответствует refresh token record.

Session list показывает пользователю и администратору активные refresh token sessions без раскрытия raw tokens.

Поля session view:

- `Id`
- `CreatedAt`
- `ExpiresAt`
- `LastUsedAt`
- `CreatedByIp`
- `UserAgent`
- `RevokedAt`

Rules:

- `GET /api/auth/sessions` возвращает active sessions текущего пользователя.
- `GET /api/auth/sessions` is implemented by `GetCurrentUserSessionsEndpoint`; it is authenticated, reads user id from the access token `sub` claim, returns only active sessions for the current user, and never returns raw refresh tokens or refresh token hashes.
- `POST /api/auth/revoke-session` is implemented by `RevokeSessionEndpoint`; it is authenticated, reads user id from the access token `sub` claim, accepts `sessionId`, and revokes only an active session owned by the current user.
- `POST /api/auth/revoke-session` is idempotent for unknown, already inactive, or other-user sessions and returns `200 OK` without exposing session ownership/existence.
- `POST /api/auth/revoke-all-sessions` отзывает все sessions текущего пользователя, включая текущую.
- `GET /api/users/{userId}/sessions` доступен администратору компании или `SystemAdmin`.
- `POST /api/users/{userId}/revoke-sessions` нужен для увольнения сотрудника, потери устройства или security incident.
- Raw refresh tokens никогда не возвращаются в API responses.
- `UserAgent` сохраняем как исходную строку, но для UI/API view дополнительно нормализуем browser, OS и device type.
- После `revoke all sessions` пользователь должен пройти login заново.
- После `revoke all sessions` отправляем email notification без tokens и без sensitive metadata.
- На MVP повторный ввод пароля перед `revoke all sessions` не требуется.
- Для первого MVP slice rate limit не реализуем.
- Отдельный persistent device fingerprint не используем.
- Approximate location по IP в session list не показываем.

Research notes:

- Auth0 treats refresh token revocation as a compromised-token scenario and supports revoking refresh tokens or full grants.
- Auth0 refresh token rotation can revoke the token family on reuse detection, forcing re-authentication.
- Google and Microsoft account UX includes device/session management and sign-out-everywhere scenarios.
- OWASP guidance for account recovery/security email flows warns against leaking sensitive data and against email flooding; notifications must be concise and rate-limited.
- Auth0 attack protection includes brute-force protection and suspicious IP throttling; this supports adding rate limits/throttling around auth-sensitive flows.
- NIST digital identity guidance discusses risk-based and step-up style authentication; for MVP we defer step-up auth/MFA until the base session model is stable.
- Google account activity can show IP addresses and approximate locations, but approximate location is best treated as helpful context, not as reliable identity proof.

## Интеграция С FileService И DirectoryService

FileService и DirectoryService должны использовать JWT bearer authentication и policy-based authorization.

Типовые checks:

- `files.read`: пользователь может читать/download metadata endpoints.
- `files.upload`: пользователь может запускать upload workflows.
- `directory.read`: пользователь может читать доступную ему структуру компании.
- `directory.manage`: пользователь может изменять структуру компании.

Business checks остаются внутри сервисов-владельцев:

- Доступ к department subtree проверяется в `DirectoryService`.
- Media ownership и доступ к file/video проверяются в `FileService`.
- AuthService не должен ходить в FileService storage или DirectoryService hierarchy во время обычной API authorization.

Первый protected flow для интеграции:

- `FileService`: защитить чтение media metadata/download metadata через `files.read`.
- `FileService`: защитить upload workflow через `files.upload`.
- `DirectoryService`: защитить чтение структуры компании через `directory.read`.
- `DirectoryService`: защитить изменение структуры компании через `directory.manage`.

Идея вопроса "какие endpoints защищаем первыми": выбрать не все API сразу, а один-два реальных пользовательских сценария. Так проще проверить JWT validation, policies, `401/403`, Swagger auth и integration tests без большого количества одновременных изменений.

## Учебные Заметки

Authentication означает доказать, кто пользователь. Authorization означает решить, что authenticated user может делать.

Access token короткоживущий, потому что обычно валидируется без database lookup. После выдачи JWT считается доверенным до expiration, если система не добавляет отдельную инфраструктуру revocation.

Refresh token хранится server-side, потому что он представляет долгоживущую session. Он должен быть revocable, rotatable и защищенным от replay.

`401 Unauthorized` означает, что запрос не authenticated или token невалиден. `403 Forbidden` означает, что пользователь authenticated, но у него недостаточно permissions для действия.

Повторяющиеся auth failure shapes держим в AuthService-local helper, а не в каждом handler-е. План блока был убрать slice-local `InvalidCredentials()`/`InvalidRefreshToken()` helpers, не меняя публичный error code. Сделано: login, refresh и `/api/auth/me` используют `AuthFailures`. Влияние: проще поддерживать одинаковую security-safe ошибку и меньше риск случайно начать раскрывать детали auth failure в одном из endpoint-ов.

### Конспект По Сегодняшним AuthService Шагам

Сегодня мы закрывали базовый session lifecycle поверх refresh token records. В MVP одна активная запись в `refresh_tokens` равна одной активной пользовательской session. API при этом говорит языком продукта: `sessions`, а не `refresh_tokens`, потому что клиенту важно управлять входами с разных устройств, а не знать внутреннюю модель хранения токенов.

`POST /api/auth/logout` завершает одну текущую refresh-token session по raw refresh token. Endpoint публичный, потому что access token к моменту logout может уже истечь, а refresh token все еще является тем секретом, который доказывает владение session. Logout идемпотентный: unknown, expired или already revoked token возвращают `200 OK`, чтобы не раскрывать, существовал ли такой token.

`POST /api/auth/revoke-all-sessions` завершает все активные sessions текущего пользователя, включая текущую. Endpoint authenticated: `userId` берется из `sub` claim access token, body не содержит `userId`. Это защищает от сценария, когда клиент мог бы передать чужой id и отозвать чужие sessions.

`GET /api/auth/sessions` показывает текущему пользователю список его активных sessions. Этот шаг нужен перед single-session revoke: frontend должен получить `sessionId`, показать пользователю список устройств/браузеров и потом отправить выбранный `sessionId` на revoke. Response содержит только безопасную metadata: `Id`, `CreatedAt`, `ExpiresAt`, `LastUsedAt`, `CreatedByIp`, `UserAgent`. Raw refresh token и `TokenHash` наружу не возвращаются.

`POST /api/auth/revoke-session` завершает одну active session текущего пользователя по `sessionId`. Ownership проверяется на сервере запросом по паре `(sessionId, userId)`. Если session не найдена, уже не active или принадлежит другому пользователю, endpoint возвращает `200 OK`. Это осознанная идемпотентность: после запроса у текущего пользователя нет active session с таким id. Возвращать `404` или `403` было бы информативнее для отладки, но хуже для security, потому что можно раскрыть существование session или ее принадлежность другому пользователю.

Если пользователь действительно не смог закрыть свою session из-за технической ошибки, это не должно маскироваться под `200 OK`: ошибка сохранения в БД идет через `ITransactionManager.SaveChangeAsync(...)` и вернет failure/500 по общей модели ошибок. Для frontend UX после `200 OK` правильный сценарий - обновить `GET /api/auth/sessions`. Если session исчезла, действие успешно. Если session осталась, UI может показать, что session все еще активна, и предложить повторить действие.

`.RequireAuthorization()` используется на authenticated Minimal API endpoints. Это аналог требования авторизации для route mapping style через `MapGet`/`MapPost`. Middleware проверяет Bearer access token до запуска handler-а. Без `.RequireAuthorization()` handler получил бы unauthenticated `ClaimsPrincipal`, `sub` не нашелся бы, и ошибка случилась бы позже как validation failure. Для такого endpoint это неверная граница ответственности: отсутствие token должно быть `401 Unauthorized`.

`[Authorize]` чаще используют в MVC controllers/actions. В нашем Slice Architecture endpoint является местом, где описаны HTTP детали: route, method, body binding, `HttpContext`, authorization requirement и маппинг claims в command/query. Handler не должен зависеть от HTTP attributes. Handler получает подготовленные данные и выполняет use case.

`AuthService.Core.Extensions.ClaimsPrincipalExtensions.GetUserId()` появился потому, что чтение `sub` claim понадобилось больше чем в одном authenticated endpoint. Мы вынесли это из slice-local private methods в AuthService-local extension. В `SharedService` это пока не выносим: выбор claim (`sub`) и auth semantics сейчас относятся к AuthService.

В repository появились отдельные методы для разных use cases. `GetActiveSessionsForUserAsync(userId)` нужен для read-only списка sessions и использует `AsNoTracking()`. `GetActiveSessionForUserAsync(sessionId, userId)` нужен для revoke одной session и возвращает tracked entity, потому что handler меняет доменную сущность через `session.Revoke(...)`, а затем сохраняет изменения через transaction manager.

Неактивные refresh token records не удаляются сразу из БД. При rotation/logout/revoke мы ставим `RevokedAt`, `RevokedByIp`, иногда `ReplacedByTokenId`. Эти записи нужны для reuse detection, audit/security history и расследования подозрительной активности. Позже можно добавить cleanup job для старых revoked/expired records, но не удалять их в момент rotation.

Мы также починили tooling warning `NU1608`: EF design tooling тянул Roslyn `4.8.0`, а analyzer packages - Roslyn `4.14.0`. Решение: выровнять Roslyn package versions и добавить private package reference на `Microsoft.CodeAnalysis.Workspaces.MSBuild`. После этого `dotnet build AuthService/AuthService.sln --no-restore` стал проходить с `0 warnings, 0 errors`.

Отдельно в SharedService backlog добавлена заметка про `clean JSON stdout`: если какой-то инструмент или helper должен отдавать machine-readable JSON, stdout не должен смешиваться с логами, баннерами, diagnostics или human-readable текстом.

## Как Защищать На Собеседовании

- AuthService владеет identity, но не всей business authorization.
- JWT validation в downstream services позволяет не ходить в AuthService на каждый запрос.
- Refresh token rotation снижает ущерб, если refresh token утек.
- Permissions as claims делают endpoint policies явными и тестируемыми.
- Полные directory/camera access trees не хранятся в JWT, потому что это делает token большим, устаревающим и high-cardinality.
- Browser cookie auth можно добавить позже, но для него нужно отдельно принять решения по CSRF, CORS, `SameSite`, `Secure` и `HttpOnly`.

## Открытые Вопросы

- Какие exact endpoints в текущих FileService и DirectoryService будут первыми защищены permissions?
- Какой минимальный seed нужен для первого `SystemAdmin`, company и первого `CompanyAdmin`?
- Когда переводим JWT signing с symmetric key на private/public key?
- Когда удаляем legacy `AuthUser` учебный slice (`/auth/users`, `auth_users`) после перехода на Identity endpoints?

## Рабочий Backlog

Ближайшие implementation tasks:

- После стабилизации refresh/invite/session domain заменить прямое создание security-sensitive entities на фабричные методы, где это даст явные инварианты (`RefreshToken.Create(...)`, invite token/session factories).
- Перенести legacy `/auth/users` registration slice на admin-created/invite flow или удалить после появления нового user management API.
- Добавить первый защищенный downstream flow в FileService/DirectoryService через permission policies.
- Подготовить private/public key JWT signing как отдельный security-hardening блок.

## Учебный Backlog

Темы, которые нужно разобрать и уметь объяснить:

- Почему AuthService использует ASP.NET Core Identity вместо собственной password/auth реализации.
- Чем отличаются authentication и authorization.
- Чем access token отличается от refresh token.
- Почему refresh token хранится в БД только как hash.
- Почему login возвращает одинаковую ошибку для missing user, wrong password и inactive user.
- Как `Issuer`, `Audience`, `SigningKey`, lifetime и `ClockSkew` участвуют в JWT validation.
- Чем symmetric JWT signing отличается от private/public key signing.
- Почему roles и permissions не являются секретами и могут быть в seed-коде.
- Зачем нужен `GET /api/auth/me`, почему frontend лучше получать self-profile через API contract, а не строить UI-state напрямую из JWT claims.
- Почему `Program.cs` держим коротким, а JWT/options/auth wiring выносим в extension methods.
- Как будет работать email flow через Mailpit в local/dev и real SMTP/email provider в production.

## Handoff

Текущее состояние AuthService:

- Identity base с `ApplicationUser`, `ApplicationRole`, roles, permissions, role-permission mapping и refresh token session model.
- Token lifecycle: login, refresh token rotation/reuse detection, logout и revoke all sessions.
- Session management: current user sessions list и revoke-session.
- Current user flow: `GET /api/auth/me` возвращает текущего пользователя, roles, permissions и company context.
- Auth failure shape централизован в AuthService-local `AuthFailures` helper для login, refresh и current-user flows.
- Build clean: AuthService build проходит без `NU1608`.

Проверки для последнего блока:

- `dotnet build AuthService/AuthService.sln --no-restore`
- `dotnet test AuthService/tests/AuthService.IntegrationTests/AuthService.IntegrationTests.csproj --no-build --filter "FullyQualifiedName~GetCurrentUserTests" --verbosity minimal`
- `dotnet test AuthService/tests/AuthService.IntegrationTests/AuthService.IntegrationTests.csproj --no-build --filter "FullyQualifiedName~LoginTests|FullyQualifiedName~RefreshTokenTests|FullyQualifiedName~GetCurrentUserTests" --verbosity minimal`
- `dotnet test AuthService/tests/AuthService.UnitTests/AuthService.UnitTests.csproj --no-build --verbosity minimal`
- `dotnet test AuthService/tests/AuthService.IntegrationTests/AuthService.IntegrationTests.csproj --no-build --verbosity minimal`

Последние проверки проходили: build `0 warnings / 0 errors`, auth failure affected integration `8/8`, unit `1/1`, integration `23/23`.

Следующий ближайший AuthService блок: заменить прямое создание security-sensitive entities на фабричные методы там, где это даст явные инварианты.

## Post-MVP Backlog

Эти улучшения не входят в первый MVP implementation slice, но должны быть видны после MVP без повторного анализа проекта с нуля.

Security hardening:

- Rate limiting для login, refresh, invite resend и notification sending.
- Account lockout / temporary throttling после серии неудачных login attempts.
- MFA или step-up auth для high-risk actions.
- Re-authentication перед изменением password, email или критичных company settings.
- Security email notification при login с нового устройства или подозрительного IP.

Session UX:

- Approximate location по IP в session list.
- Более качественный device display name.
- Возможность назвать trusted device.

Enterprise/B2B:

- Verified company email domains как optional настройка.
- SSO/SAML/OIDC federation для крупных клиентов.
- Multi-company membership для холдингов, подрядчиков и операторов нескольких клиентов.
- `UserCompanyMembership` вместо одного `CurrentCompanyId` в `ApplicationUser`.

Audit/Compliance:

- Отдельные audit filters по company/user/action/date.
- Export audit events для company admins.
- Retention policy для audit events.

Implementation notes:

- Rate limiting перед публичным stage лучше добавить обязательно: OWASP API Security относит отсутствие resource/rate limiting к отдельному классу API risks.
- MFA/step-up auth лучше добавлять после стабильной базовой модели users, sessions и refresh token rotation.
- Approximate location показывать только как "примерное местоположение", не использовать как security proof.

## Research References

- Auth0 refresh token revocation: https://auth0.com/docs/tokens/guides/revoke-refresh-tokens
- Auth0 refresh token rotation and reuse detection: https://auth0.com/docs/security/tokens/refresh-tokens/configure-refresh-token-rotation
- Auth0 suspicious IP throttling: https://auth0.com/docs/attack-protection/suspicious-ip-throttling
- Auth0 Organizations: https://auth0.com/docs/manage-users/organizations
- Clerk Organizations overview: https://clerk.com/docs/guides/organizations/overview
- Google account device/session management: https://support.google.com/accounts/answer/3067630
- Microsoft sign out everywhere: https://support.microsoft.com/en-gb/account-billing/how-to-sign-out-of-your-microsoft-account-everywhere-58da4a74-a719-43a6-9dd0-74a7e613229f
- OWASP forgot password/security email guidance: https://cheatsheetseries.owasp.org/cheatsheets/Forgot_Password_Cheat_Sheet.html
- NIST Digital Identity Guidelines: https://pages.nist.gov/800-63-4/
