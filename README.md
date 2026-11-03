# FridmakWeb

Веб-приложение на ASP.NET Core MVC: регистрация и профили, друзья, чат в реальном времени,
чат-боты и модерация. Учебный проект, в котором я собрал вместе аутентификацию,
работу с БД, фоновые задачи и собственные middleware.

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
- Друзья: заявки, принятие, список.
- Чат: отправка, редактирование и удаление сообщений, история.
- Чат-боты: цензор, помощник, мем-бот. Боты находятся в чате как обычные пользователи.
- Модерация: список запрещённых слов в `Resources/BannedChatWords.txt`.
- Rate limiting: 1000 запросов в минуту с одного IP, ответ 429.
- IP-фильтр: блокировка нежелательных адресов до попадания в контроллеры.
- Обработка ошибок: отдельный контроллер и страницы ошибок.

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

1. Создай базу SQL Server и укажи строку подключения в `appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=FridmakWeb;Trusted_Connection=True;TrustServerCertificate=True"
}
```

2. Примени миграции:

```bash
dotnet ef database update
```

3. Запусти:

```bash
dotnet run
```

4. Открой `https://localhost:5001` (порт смотри в `Properties/launchSettings.json`).

## Что бы улучшил

- Вынести чат в отдельный сервис с SignalR вместо polling.
- Добавить интеграционные тесты на контроллеры и middleware.
- Заменить in-memory rate limiter на Redis, чтобы лимит работал между репликами.
- Добавить Docker Compose с SQL Server.

## Статус

Учебный проект, писал для практики ASP.NET Core MVC и работы с EF Core.
Код — в ветке `master`.
