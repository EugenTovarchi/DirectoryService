# AuthService Roadmap

Этот файл - короткий хронологический журнал развития AuthService: что сделали, зачем, что это дало и куда двигаться дальше. Подробная архитектура остается в `backend/docs/services/auth-service.md`, а здесь держим рабочую картину по шагам.

## Текущее Состояние

- ASP.NET Core Identity с `ApplicationUser` и `ApplicationRole`.
- Seed ролей, permissions и role-permission связей.
- Login, access JWT, refresh token rotation, reuse detection, logout.
- Self-session management: list, revoke one session, revoke all sessions.
- Admin user management: invite, list, details, status, role, sessions, revoke sessions.
- Invite token lifecycle без `InitialPassword`: pending user, one-time invite token на 3 дня, accept invite.
- Security-sensitive commands используют явный transaction scope по FS/DS-style паттерну.

## Архитектурные Правила

- Commands/state changes: Identity и EF Core.
- Query/read-heavy endpoints: Dapper + `INpgsqlConnectionFactory`.
- Authenticated endpoints читают user id через `ClaimsPrincipalExtensions.GetUserId()`.
- Raw refresh/invite tokens не храним в БД и не логируем.
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

## Ближайший План

1. Resend invite endpoint:
   - отозвать текущий pending invite;
   - создать новый invite token на 3 дня;
   - вернуть raw token для local/dev до email delivery.

2. User profile edit:
   - редактировать safe поля пользователя;
   - не смешивать с role/status/password flows.

3. Email delivery integration:
   - Mailpit для local/dev;
   - real SMTP/provider позже;
   - raw invite token только в ссылке, без логов.

4. Password reset:
   - отдельный token lifecycle;
   - одинаковые public failure shapes;
   - rate limiting до public stage.

5. Audit history:
   - invite created/accepted/revoked;
   - role/status changes;
   - session revocation.

6. Downstream permission integration:
   - первые protected flows в FileService и DirectoryService;
   - проверить `401/403`, policies и Swagger auth.

## Открытые Решения

- Когда переходить с symmetric JWT signing на private/public key signing.
- Какой минимальный seed нужен для первого `SystemAdmin`, company и первого `CompanyAdmin`.
- Нужен ли отдельный generic token hashing service вместо текущего refresh-token-oriented naming.
- Какой audit/event model брать для security history.
