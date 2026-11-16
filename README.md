# FridmakWeb

Веб-приложение на ASP.NET Core MVC: регистрация и профили, друзья, чат в реальном времени,
чат-боты и модерация. Полный цикл в одном проекте — от схемы данных и миграций
до фоновых сервисов, собственных middleware и обработки ошибок.

## Стек

- .NET 8, ASP.NET Core MVC
- Entity Framework Core 9 + PostgreSQL (Npgsql)
- Cookie-аутентификация, BCrypt для паролей
- Фоновый сервис (`BackgroundService`) для обработки сообщений
- Собственные middleware: rate limiting и IP-фильтр
- HTML/CSS/JS, Razor

## Возможности

- Регистрация и логин, хеширование паролей через BCrypt.
- Профиль пользователя: bio и редактирование данных.
- Друзья: глобальный список контактов с заявками и принятием (в планах — связи между пользователями).
- Чат: отправка, редактирование и удаление сообщений, история.
- Чат-боты: цензор, помощник, мем-бот. Боты находятся в чате как обычные пользователи.
- Модерация: список запрещённых слов в `Resources/BannedChatWords.txt`.
- Rate limiting: настраиваемый лимит на IP (по умолчанию 1000 запросов в минуту), ответ 429.
- IP-фильтр: блокировка нежелательных адресов до попадания в контроллеры, ответ 403.
- Обработка ошибок: отдельный контроллер и страницы ошибок.

## Безопасность

- Чат и страницы друзей закрыты `[Authorize]`.
- Редактировать и удалять сообщение может только его автор или администратор.
- POST-запросы чата и формы защищены antiforgery-токеном.
- Cookie аутентификации: `HttpOnly`, `SameSite=Lax`, срок жизни 2 часа.
- Секреты не хранятся в `appsettings.json`: пароль ботов генерируется автоматически,
  если `BotSettings:DefaultPassword` не задан через переменные окружения.
- Rate limit и IP-фильтр возвращают корректные статусы `429` и `403`, а не `302`.

## Архитектура

```
Controllers/        Home, User, Chat, Error
Models/             User, Friend, ChatMSG, view models
Data/               AppDbContext
Services/           UserService, HomeService, ChatService
Services/Chat/      ChatServer, ChatHandler, ChatBackgroundService
Bots/ChatBots/      ChatBotsManager, ChatBotHandler
Services/Chat/Bots/ CensorshipBot, HelpBot, MemeBot
Middleware/         IpFilterMiddleware, RateLimitMiddleware
Migrations/         миграции EF Core
```

Чат обрабатывается через `ChatHandler` и фоновый сервис: боты и пользователи
получают сообщения через него. Состояние сообщений хранится в БД,
поэтому история не теряется при перезапуске.

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

1. Создай `.env` из примера и задай свои пароли:

```bash
cp .env.example .env
```

В PowerShell:

```powershell
Copy-Item .env.example .env
```

2. Подними стек:

```bash
docker compose up --build -d
```

3. Открой:

- приложение — http://localhost:8081
- PostgreSQL — `localhost:5434`, пользователь и пароль из `POSTGRES_USER` и `POSTGRES_PASSWORD`
- админ — логин и пароль из `ADMIN_USERNAME` и `ADMIN_PASSWORD`

Миграции применяются на старте. Если админа ещё нет, он создаётся автоматически.
Порты `8081` и `5434` выбраны, чтобы не конфликтовать с Microservices (`8080` и `5433`).

4. Полезные команды:

```bash
docker compose ps
docker compose logs -f app
docker compose down
docker compose down -v   # вместе с базой
```

## Дальнейшие шаги

- Перевести чат на SignalR вместо polling — подробная инструкция в [`docs/signalr-migration.md`](docs/signalr-migration.md).
- Сделать друзей связями между пользователями, а не глобальным списком контактов.
- Добавить WasmGate, Prometheus и Grafana, страницу метрик в админке.
- Добавить интеграционные тесты на контроллеры, middleware и права доступа.
- Вынести rate limiter в Redis, чтобы лимит работал между репликами.
- Опционально: ClickHouse для аналитики событий чата и админ-отчётов.

## Чему научился

- Собирать приложение целиком: от схемы БД и миграций до middleware и обработки ошибок.
- Держать состояние чата в БД через `BackgroundService`, чтобы история не терялась при перезапуске.
- Писать собственные middleware и понимать порядок их выполнения в конвейере.
