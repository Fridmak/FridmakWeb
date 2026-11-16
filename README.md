# FridmakWeb

Веб-приложение на ASP.NET Core MVC: регистрация и профили, друзья, чат в реальном времени,
чат-боты и модерация. Полный цикл в одном проекте — от схемы данных и миграций
до фоновых сервисов, собственных middleware и обработки ошибок.

## Стек

- .NET 8, ASP.NET Core MVC
- Entity Framework Core 9 + PostgreSQL (Npgsql)
- Cookie-аутентификация, BCrypt для паролей
- Фоновый сервис (`BackgroundService`) для обработки сообщений
- WasmGate (L4/TCP edge) + Prometheus + Grafana
- Собственные middleware: rate limiting и IP-фильтр
- HTML/CSS/JS, Razor

## Возможности

- Регистрация и логин, хеширование паролей через BCrypt.
- Профиль пользователя: bio, редактирование данных, просмотр чужих профилей.
- Друзья: поиск пользователей, заявки, принятие/отклонение, список друзей и удаление.
- Чат: отправка, редактирование и удаление сообщений, история.
- Чат-боты: цензор, помощник, мем-бот. Боты находятся в чате как обычные пользователи.
- Модерация: список запрещённых слов в `Resources/BannedChatWords.txt`.
- Rate limiting: настраиваемый лимит на IP (по умолчанию 1000 запросов в минуту), ответ 429.
- IP-фильтр: блокировка нежелательных адресов до попадания в контроллеры, ответ 403.
- Обработка ошибок: отдельный контроллер и страницы ошибок.

## Безопасность

- Чат и страницы друзей закрыты `[Authorize]`.
- Редактировать и удалять сообщение может только его автор или администратор.
- Друзья — это связи между пользователями, а не глобальный список контактов.
- Email и роль видны только владельцу профиля и администратору.
- POST-запросы чата и формы защищены antiforgery-токеном.
- Cookie аутентификации: `HttpOnly`, `SameSite=Lax`, срок жизни 2 часа.
- Секреты не хранятся в `appsettings.json`: пароль ботов генерируется автоматически,
  если `BotSettings:DefaultPassword` не задан через переменные окружения.
- Rate limit и IP-фильтр возвращают корректные статусы `429` и `403`, а не `302`.

## Архитектура

```
Controllers/
  Account/    вход, регистрация, выход
  Admin/      админка, боты, метрики
  Chat/       чат
  Error/      страницы ошибок
  Friends/    друзья и заявки
  Home/       главная, about, error
  Profile/    профиль и редактирование

Models/                User, Friendship, Chat/ChatMessage, ViewModels/
Data/                  AppDbContext
Services/
  Users/               UserService
  Friends/             FriendshipService
  Seed/                AdminSeeder
  Observability/       AppMetrics, PrometheusMetricsService
  Chat/                ChatService, ChatMessageProcessor, ChatSessionManager,
                       ChatUserSession, ChatBackgroundService
  Chat/Bots/           IChatBot, ChatBotManager, ChatBotHandler,
                       CensorshipBot, HelpBot, MemeBot

Options/               BotOptions, RateLimitOptions, IpFilterOptions, SeedOptions,
                       MetricsOptions, PrometheusOptions
Extensions/            ClaimsPrincipalExtensions, DatabaseExtensions
Middleware/            IpFilterMiddleware, RateLimitMiddleware
deploy/                wasmgate/, observability/{prometheus,grafana}
Migrations/            миграции EF Core
```

Чат обрабатывается через `ChatService`, `ChatMessageProcessor` и `ChatSessionManager`:
сообщения сохраняются в PostgreSQL, а боты получают их через `ChatUserSession`.
История не теряется при перезапуске приложения.

## Как запустить

1. Установи .NET 8 SDK и PostgreSQL (проще всего — через Docker Compose, см. ниже).

2. Укажи строку подключения в `appsettings.json` или через переменную окружения:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5434;Database=fridmakweb;Username=fridmak;Password=change_me"
}
```

Для локального запуска удобнее задать строку подключения через окружение:

```powershell
$env:ConnectionStrings__DefaultConnection="Host=localhost;Port=5434;Database=fridmakweb;Username=fridmak;Password=..."
dotnet run --urls http://localhost:5295
```

3. Примени миграции:

```bash
dotnet ef database update
```

4. Открой `http://localhost:5295`.

Для HTTPS-профиля используй `https://localhost:7168` (порт из `Properties/launchSettings.json`).

Лимит запросов настраивается в `appsettings.json`:

```json
"RateLimit": {
  "Limit": 1000,
  "WindowSeconds": 60
}
```

## Запуск в Docker

Нужен Docker Desktop с Linux-контейнерами.

Стек:

```text
client
  │
  ▼
WasmGate :8081            L4/TCP edge, rate limit, metrics :9099
  │
  ▼
FridmakWeb :8080          MVC + /metrics (Bearer METRICS_TOKEN)
  │
  ├── PostgreSQL :5432
  │
  └── Prometheus :9090 ── Grafana :3000
```

1. Первый запуск. Скрипт создаст `.env` со случайными секретами и поднимет стек:

```powershell
./scripts/up.ps1
```

Либо вручную:

```powershell
Copy-Item .env.example .env
# отредактируй пароли в .env
docker compose up --build -d
```

2. Открой:

| Что | Адрес |
|---|---|
| Приложение через WasmGate | http://localhost:8081 |
| Приложение напрямую (debug) | http://localhost:8082 |
| Grafana | http://localhost:3000 |
| Prometheus | http://localhost:9091 |
| WasmGate admin (`/healthz`, `/metrics`) | http://localhost:19099 |
| PostgreSQL | localhost:5434 |

Логин админа — `ADMIN_USERNAME` / `ADMIN_PASSWORD` из `.env`.
Grafana — `GRAFANA_ADMIN_USER` / `GRAFANA_ADMIN_PASSWORD` из `.env`.

3. Админка и метрики:

- `http://localhost:8081/Admin` — управление ботами;
- `http://localhost:8081/Admin/Metrics` — карточки из Prometheus и ссылка на Grafana.

4. WasmGate:

- используется ветка `metrics` (закреплён коммит `8aa95f56...`);
- пересобрать с актуальной веткой:

```powershell
$env:WASMGATE_REF="metrics"
docker compose build wasmgate
docker compose up -d wasmgate
```

- admin-порт `19099` опубликован только на loopback и защищён `WASMGATE_ADMIN_TOKEN`;
- edge rate limit настроен в `deploy/wasmgate/wasmgate.yaml`;
- приложение за L4-прокси видит IP WasmGate, поэтому app-level `RateLimit` и
  `IpFilter` в compose отключены (`RateLimit__Enabled=false`, `IpFilter__Enabled=false`).
  Для прямого запуска без WasmGate они остаются включёнными.

5. Метрики:

- приложение отдаёт `/metrics` с `prometheus-net`: HTTP, чат, регистрации, сессии;
- `/metrics` защищён `METRICS_TOKEN` и не должен быть публичным;
- WasmGate отдаёт свои L4-метрики на `:9099/metrics` с `WASMGATE_ADMIN_TOKEN`;
- Prometheus скрапит оба endpoint’а, Grafana поднимает готовый дашборд
  `deploy/observability/grafana/dashboards/fridmakweb.json`.

6. Полезные команды:

```bash
docker compose ps
docker compose logs -f app wasmgate prometheus grafana
docker compose down
docker compose down -v   # вместе с БД и метриками
```

## Дальнейшие шаги

- Перевести чат на SignalR вместо polling — подробная инструкция в [`docs/signalr-migration.md`](docs/signalr-migration.md).
- Добавить интеграционные тесты на контроллеры, middleware и права доступа.
- Вынести rate limiter в Redis, чтобы лимит работал между репликами.
- Опционально: ClickHouse для аналитики событий чата и админ-отчётов.
- Опционально: WasmGate Control Plane для управления несколькими edge-нодами.

## Чему научился

- Собирать приложение целиком: от схемы БД и миграций до middleware и обработки ошибок.
- Держать состояние чата в БД через `BackgroundService`, чтобы история не терялась при перезапуске.
- Писать собственные middleware и понимать порядок их выполнения в конвейере.
