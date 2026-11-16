# FridmakWeb

Веб-приложение на ASP.NET Core MVC: регистрация и профили, друзья, чат в реальном времени,
чат-боты и модерация. Полный цикл в одном проекте — от схемы данных и миграций
до фоновых сервисов, собственных middleware и обработки ошибок.

## Стек

- .NET 8, ASP.NET Core MVC
- Entity Framework Core 9 + SQL Server
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

1. Установи .NET 8 SDK и SQL Server LocalDB (или обычный SQL Server).

2. Укажи строку подключения в `appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=TestingAppWebDb;Trusted_Connection=True;MultipleActiveResultSets=true"
}
```

3. Примени миграции (если `dotnet ef` ещё не установлен — `dotnet tool install --global dotnet-ef`):

```bash
dotnet ef database update
```

4. Запусти:

```bash
dotnet run --urls http://localhost:5295
```

5. Открой `http://localhost:5295`.

Для HTTPS-профиля используй `https://localhost:7168` (порт из `Properties/launchSettings.json`).

Лимит запросов настраивается в `appsettings.json`:

```json
"RateLimit": {
  "Limit": 1000,
  "WindowSeconds": 60
}
```

## Дальнейшие шаги

- Docker Compose с SQL Server и одной командой запуска.
- Перевести чат на SignalR вместо polling.
- Сделать друзей связями между пользователями, а не глобальным списком контактов.
- Добавить WasmGate, Prometheus и Grafana, страницу метрик в админке.
- Добавить интеграционные тесты на контроллеры, middleware и права доступа.
- Вынести rate limiter в Redis, чтобы лимит работал между репликами.

## Чему научился

- Собирать приложение целиком: от схемы БД и миграций до middleware и обработки ошибок.
- Держать состояние чата в БД через `BackgroundService`, чтобы история не терялась при перезапуске.
- Писать собственные middleware и понимать порядок их выполнения в конвейере.
