# AuthService Roadmap

Этот файл - короткий хронологический журнал развития AuthService: что сделали, зачем, что это дало и куда двигаться дальше. Подробная архитектура остается в `backend/docs/services/auth-service.md`, а здесь держим рабочую картину по шагам.

## Текущее Состояние

- ASP.NET Core Identity с `ApplicationUser` и `ApplicationRole`.
- Seed ролей, permissions и role-permission связей.
- Login, access JWT, refresh token rotation, reuse detection, logout.
- Self-session management: list, revoke one session, revoke all sessions.
- Admin user management: invite, resend invite, list, details, status, role, sessions, revoke sessions.
- Invite token lifecycle без `InitialPassword`: pending user, one-time invite token на 3 дня, accept invite.
- Security-sensitive commands используют явный transaction scope по FS/DS-style паттерну.

## Архитектурные Правила

- Commands/state changes: Identity и EF Core.
- Query/read-heavy endpoints: Dapper + `INpgsqlConnectionFactory`.
- Authenticated endpoints читают user id через `ClaimsPrincipalExtensions.GetUserId()`.
- Raw refresh/invite tokens не храним в БД и не логируем; raw invite token наружу выходит только внутри invite link.
- Password hash, security stamp, token hashes и session secrets наружу не возвращаем.
- Для sensitive flows используем `ITransactionManager.BeginTransactionAsync(...)`, `ITransactionScope`, `SaveChangeAsync(...)`, `Commit()`.
- AuthService коммиты: сначала показать изменения, потом краткая сводка, потом спросить коммитить или нет.
- При AuthService-коммитах обновляем этот roadmap, если коммит добавляет фичу, меняет flow, архитектурное правило или ближайший план.

## Хронология Фич

<details>
<summary>1. Identity base</summary>

**Зачем:** заменить учебную пользовательскую модель нормальной auth-базой.

**Сделано:**
- Добавлены `ApplicationUser`, `ApplicationRole`.
- Подключены ASP.NET Core Identity tables.
- Password hashing передан Identity.

**Что дало:** единая production-like модель пользователей, паролей и ролей вместо самописного `AuthUser`.

</details>

<details>
<summary>2. Roles и permissions seed</summary>

**Зачем:** получить управляемую модель authorization без hard-coded checks в каждом endpoint.

**Сделано:**
- Seed roles: `SystemAdmin`, `CompanyAdmin`, `Operator`, `Technician`, `Viewer`.
- Seed Auth/File/Directory permissions.
- Role-permission mapping создается runtime seeder-ом.

**Что дало:** JWT может содержать coarse-grained permissions, а endpoints могут опираться на policies.

</details>

<details>
<summary>3. Login и access JWT</summary>

**Зачем:** дать frontend и downstream services стандартный Bearer auth flow.

**Сделано:**
- `POST /api/auth/login`.
- Проверка credentials через `UserManager`.
- Access JWT с user, company, roles и permissions claims.
- Единая security-safe ошибка для missing user, wrong password и inactive user.

**Что дало:** AuthService начал выдавать access tokens, которые FileService/DirectoryService смогут валидировать локально.

</details>

<details>
<summary>4. Refresh token storage</summary>

**Зачем:** поддержать долгоживущие sessions без хранения access token на сервере.

**Сделано:**
- Refresh token хранится server-side как hash.
- Raw refresh token возвращается клиенту только один раз.
- Session metadata сохраняет timestamps, IP и user agent.

**Что дало:** появилась revocable session model, пригодная для logout, rotation и списка devices.

</details>

<details>
<summary>5. Refresh rotation и reuse detection</summary>

**Зачем:** снизить ущерб при утечке refresh token.

**Сделано:**
- `POST /api/auth/refresh`.
- Каждый refresh отзывает старый token и создает replacement.
- Повторная отправка уже revoked/replaced token считается reuse и отзывает активные sessions пользователя.

**Что дало:** refresh lifecycle стал ближе к production security модели.

</details>

<details>
<summary>6. Logout и revoke all sessions</summary>

**Зачем:** дать пользователю способ завершить одну session или все sessions.

**Сделано:**
- `POST /api/auth/logout` по raw refresh token.
- `POST /api/auth/revoke-all-sessions` для текущего authenticated user.
- Logout идемпотентен для unknown/expired/revoked token.

**Что дало:** пользователь может контролировать lifecycle своих sessions без раскрытия token state.

</details>

<details>
<summary>7. Current user sessions</summary>

**Зачем:** дать UI список активных входов перед выборочным revoke.

**Сделано:**
- `GET /api/auth/sessions`.
- `POST /api/auth/revoke-session`.
- Ownership проверяется сервером по `(sessionId, currentUserId)`.

**Что дало:** появился безопасный self-service device/session management.

</details>

<details>
<summary>8. GET /api/auth/me</summary>

**Зачем:** не заставлять frontend строить UI-state напрямую из JWT claims.

**Сделано:**
- `GET /api/auth/me`.
- Response содержит id, email, username, display name, company context, roles и permissions.

**Что дало:** frontend получил стабильный self-profile contract для app shell, navigation и permission gates.

</details>

<details>
<summary>9. User invite MVP</summary>

**Зачем:** дать CompanyAdmin/SystemAdmin первый способ создавать Identity users.

**Сделано:**
- `POST /api/users/invite`.
- Endpoint требует `users.manage`.
- Создавал Identity user с company context и role.
- На первом этапе временно использовал `InitialPassword`.

**Что дало:** появился первый Identity-based user management flow.

</details>

<details>
<summary>10. Legacy /auth/users removal</summary>

**Зачем:** убрать параллельную учебную user model рядом с Identity.

**Сделано:**
- Удалены legacy `/auth/users`, `AuthUser`, legacy repository/contracts/tests.
- EF migration удаляет `auth_users`.

**Что дало:** дальше AuthService развивается только через Identity users, roles, permissions и invite lifecycle.

</details>

<details>
<summary>11. Admin user directory</summary>

**Зачем:** дать admin UI список пользователей.

**Сделано:**
- `GET /api/users?page=1&pageSize=20`.
- Response: `PagedList<CompanyUserResponse>`.
- Read-side через Dapper.
- `SystemAdmin` видит все companies, `CompanyAdmin` только свою company.

**Что дало:** появилась плоская user directory без смешивания с DirectoryService hierarchy.

</details>

<details>
<summary>12. Admin user details</summary>

**Зачем:** дать admin UI карточку пользователя без write-actions.

**Сделано:**
- `GET /api/users/{userId}`.
- Response: `CompanyUserDetailsResponse`.
- Safe fields: id, email, username, displayName, companyId, isActive, roles, createdAt, updatedAt.

**Что дало:** UI может открывать детальную карточку без exposure password/security/session data.

</details>

<details>
<summary>13. Change user status</summary>

**Зачем:** поддержать activate/deactivate пользователя, например offboarding.

**Сделано:**
- `PATCH /api/users/{userId}/change-status`.
- Deactivate отзывает active refresh sessions.
- Self-deactivation запрещен.

**Что дало:** администратор может отключить учетку, а refresh lifecycle сразу блокируется.

</details>

<details>
<summary>14. Change user role</summary>

**Зачем:** менять обязанности пользователя без отключения account.

**Сделано:**
- `PATCH /api/users/{userId}/change-role`.
- Заменяет текущую роль на одну существующую.
- CompanyAdmin не может назначить `SystemAdmin`.
- Self-role-change запрещен.

**Что дало:** admin UI может повышать/понижать пользователя между MVP roles отдельно от status/offboarding.

</details>

<details>
<summary>15. Admin revoke sessions</summary>

**Зачем:** завершать sessions другого пользователя при потере устройства, увольнении или security incident.

**Сделано:**
- `POST /api/users/{userId}/revoke-sessions`.
- Endpoint требует `users.manage`.
- Self-flow остается на `/api/auth/revoke-all-sessions`.

**Что дало:** администратор получил ручной security control без изменения status/role.

</details>

<details>
<summary>16. Admin user sessions list</summary>

**Зачем:** показать admin UI активные sessions пользователя перед revoke.

**Сделано:**
- `GET /api/users/{userId}/sessions`.
- Возвращает safe session metadata.
- Raw refresh tokens и hashes наружу не возвращаются.

**Что дало:** администратор может видеть devices/sessions пользователя и принимать точечные решения.

</details>

<details>
<summary>17. Invite token lifecycle</summary>

**Зачем:** убрать `InitialPassword`, чтобы администратор не задавал пароль за пользователя.

**Сделано:**
- Invite создает inactive user без password.
- Генерируется one-time invite token на 3 дня.
- В БД хранится только hash invite token.
- `POST /api/auth/accept-invite` принимает token/password, активирует user и помечает token accepted.

**Что дало:** pending user не может login до принятия invite, raw invite token не хранится, ошибки unknown/expired/reused invite остаются security-safe.

</details>

<details>
<summary>18. Command transaction scopes</summary>

**Зачем:** сделать token/password/user-management flows атомарными.

**Сделано:**
- Добавлены `ITransactionScope` и `ITransactionManager.BeginTransactionAsync(...)`.
- Postgres implementation повторяет FS/DS-style pattern.
- Transaction scopes добавлены в sensitive commands: invite, accept invite, login, refresh, logout, revoke sessions, change status, change role.

**Что дало:** связанные изменения вроде `user + role + invite token` и `password + activate + accepted token` выполняются в одной явной transaction boundary.

</details>

<details>
<summary>19. Resend invite</summary>

**Зачем:** дать администратору способ выдать новый invite, если первичная ссылка потеряна или истекла, без создания второго пользователя.

**Сделано:**
- `POST /api/users/{userId}/resend-invite`.
- Только inactive user без password.
- Active pending invite tokens отзываются перед выпуском нового token.
- Новый invite token живет 3 дня, в БД хранится только hash.

**Что дало:** invite lifecycle поддерживает повторную выдачу секрета, не логируя raw token и не раскрывая users из другой company.

</details>

<details>
<summary>20. Invite email delivery</summary>

**Зачем:** убрать raw invite token из API response и доставлять secret по ожидаемому onboarding каналу.

**Сделано:**
- SMTP abstraction для invite emails.
- `POST /api/users/invite` и `POST /api/users/{userId}/resend-invite` отправляют invite link после commit.
- Docker local-dev получил Mailpit: SMTP `mailpit:1025`, UI `http://localhost:8025`.
- API responses больше не содержат standalone raw invite token.

**Что дало:** invite/resend стали ближе к production flow: raw token есть только в ссылке, не хранится в БД и не логируется.

</details>

<details>
<summary>21. Password reset flow</summary>

**Зачем:** дать пользователю восстановить доступ без помощи администратора и без раскрытия существования email.

**Сделано:**
- `POST /api/auth/request-password-reset`.
- `POST /api/auth/reset-password`.
- Отдельная `PasswordResetToken` entity/table.
- Raw reset token не хранится, в БД только hash.
- Reset token живет 1 час.
- Unknown email/inactive/no-password request возвращает тот же `200 OK`.
- Unknown/expired/revoked/used token возвращает `password.reset.token.is.invalid`.
- Успешный reset меняет Identity password, помечает token used и отзывает active refresh sessions.

**Что дало:** AuthService получил public password recovery flow без переиспользования invite tokens и без token/user enumeration.

</details>

<details>
<summary>22. Admin user profile edit</summary>

**Зачем:** дать admin UI безопасное редактирование пользовательских полей без смешивания с role/status/password flows.

**Сделано:**
- `PATCH /api/users/{userId}/profile`.
- Endpoint требует `users.manage`.
- Current slice обновляет только `displayName`.
- `displayName: null` очищает display name.
- CompanyAdmin ограничен своей company, SystemAdmin может менять users из любой company.
- Response остается `CompanyUserDetailsResponse`.

**Что дало:** малорисковый profile edit отделен от high-risk actions: role, status, password, sessions и company context.

</details>

## Ближайший План

1. Audit history:
   - invite created/accepted/revoked;
   - password reset requested/completed;
   - profile changes;
   - role/status changes;
   - session revocation.

2. Downstream permission integration:
   - первые protected flows в FileService и DirectoryService;
   - проверить `401/403`, policies и Swagger auth.

3. Invite/password reset email outbox/retry hardening:
   - записывать email delivery job в той же transaction, что и invite/resend/reset token;
   - background worker отправляет SMTP и делает retry/backoff;
   - не хранить raw invite/reset token отдельно от delivery payload дольше нужного срока;
   - не логировать raw token, link или SMTP credentials;
   - делать перед production-grade delivery, не блокирует текущий MVP.

## Открытые Решения

- Когда переходить с symmetric JWT signing на private/public key signing.
- Какой минимальный seed нужен для первого `SystemAdmin`, company и первого `CompanyAdmin`.
- Нужен ли отдельный generic token hashing service вместо текущего refresh-token-oriented naming.
- Какой audit/event model брать для security history.
