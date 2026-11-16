# Перевод чата FridmakWeb на SignalR

Это пошаговая инструкция для самостоятельной реализации. Она написана под текущий код проекта:
`ChatController` + polling каждые 500 мс, `ChatUserSession`/`ChatSessionManager`, `ChatMessageProcessor`,
`ChatBackgroundService` и боты в `Services/Chat/Bots`.

Цель: сохранить сообщения в PostgreSQL **до** отправки клиентам, убрать polling,
оставить ботов и получить нормальный real-time чат.

> Важно: не делай всё одним коммитом. Иди по шагам и после каждого шага проверяй,
> что чат, редактирование, удаление и боты продолжают работать.

---

## 0. Ветка и план

```bash
git switch -c feat/signalr-chat
```

Порядок работ:

1. Вынести работу с сообщениями в отдельный сервис.
2. Добавить `ChatHub`.
3. Подключить SignalR в `Program.cs`.
4. Перевести ботов на очередь событий.
5. Переписать фронтенд с polling на SignalR.
6. Добавить аудит сообщений и миграцию.
7. Написать тесты.
8. Удалить старый polling-код.

Ориентировочно: **3–4 дня** спокойной работы.

---

## 1. Сервис сообщений

Сейчас логика размазана между `ChatService`, `ChatMessageProcessor`, `ChatUserSession` и контроллером.
Для SignalR нужен один сервис, который:

- проверяет авторизацию и владельца;
- сохраняет сообщение в БД;
- возвращает DTO;
- не знает про HTTP и SignalR.

Создай `Services/Chat/IChatMessageService.cs` и `Services/Chat/ChatMessageService.cs`.

```csharp
using TestingAppWeb.Models.Chat;

namespace TestingAppWeb.Services.Chat;

public enum ChatOperationStatus
{
    Success,
    NotFound,
    Forbidden,
    Invalid
}

public sealed record ChatOperationResult(
    ChatOperationStatus Status,
    ChatMessageDto? Message = null);

public interface IChatMessageService
{
    Task<ChatOperationResult> SendAsync(int userId, string userName, string text);
    Task<ChatOperationResult> EditAsync(int userId, bool isAdmin, int messageId, string text);
    Task<ChatOperationResult> DeleteAsync(int userId, bool isAdmin, int messageId);
    Task<IReadOnlyList<ChatMessageDto>> GetHistoryAsync(int take = 50, int? beforeId = null);
}
```

Пример реализации:

```csharp
using Microsoft.EntityFrameworkCore;
using TestingAppWeb.Data;
using TestingAppWeb.Models.Chat;

namespace TestingAppWeb.Services.Chat;

public sealed class ChatMessageService : IChatMessageService
{
    private const int MaxMessageLength = 2000;

    private readonly AppDbContext _db;

    public ChatMessageService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ChatOperationResult> SendAsync(int userId, string userName, string text)
    {
        text = text?.Trim() ?? string.Empty;

        if (text.Length is < 1 or > MaxMessageLength)
        {
            return new ChatOperationResult(ChatOperationStatus.Invalid);
        }

        var userExists = await _db.Users.AnyAsync(user => user.Id == userId);
        if (!userExists)
        {
            return new ChatOperationResult(ChatOperationStatus.Forbidden);
        }

        var message = new ChatMessage
        {
            SenderId = userId,
            MessageText = text,
            SentAt = DateTimeOffset.UtcNow
        };

        _db.ChatMessages.Add(message);
        await _db.SaveChangesAsync();

        return new ChatOperationResult(
            ChatOperationStatus.Success,
            new ChatMessageDto
            {
                MessageId = message.Id,
                Text = message.MessageText,
                UserName = userName,
                Timestamp = message.SentAt
            });
    }

    public async Task<ChatOperationResult> EditAsync(
        int userId,
        bool isAdmin,
        int messageId,
        string text)
    {
        text = text?.Trim() ?? string.Empty;

        if (text.Length is < 1 or > MaxMessageLength)
        {
            return new ChatOperationResult(ChatOperationStatus.Invalid);
        }

        var message = await _db.ChatMessages.FindAsync(messageId);
        if (message is null)
        {
            return new ChatOperationResult(ChatOperationStatus.NotFound);
        }

        if (message.SenderId != userId && !isAdmin)
        {
            return new ChatOperationResult(ChatOperationStatus.Forbidden);
        }

        message.MessageText = text;

        // Если добавишь поля аудита:
        // message.EditedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();

        return new ChatOperationResult(ChatOperationStatus.Success);
    }

    public async Task<ChatOperationResult> DeleteAsync(
        int userId,
        bool isAdmin,
        int messageId)
    {
        var message = await _db.ChatMessages.FindAsync(messageId);
        if (message is null)
        {
            return new ChatOperationResult(ChatOperationStatus.NotFound);
        }

        if (message.SenderId != userId && !isAdmin)
        {
            return new ChatOperationResult(ChatOperationStatus.Forbidden);
        }

        _db.ChatMessages.Remove(message);

        // Мягкое удаление лучше:
        // message.IsDeleted = true;
        // message.DeletedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();

        return new ChatOperationResult(ChatOperationStatus.Success);
    }

    public async Task<IReadOnlyList<ChatMessageDto>> GetHistoryAsync(
        int take = 50,
        int? beforeId = null)
    {
        take = Math.Clamp(take, 1, 200);

        var query = _db.ChatMessages
            .AsNoTracking()
            .Where(message => !string.IsNullOrEmpty(message.MessageText));

        if (beforeId is not null)
        {
            query = query.Where(message => message.Id < beforeId);
        }

        var messages = await query
            .OrderByDescending(message => message.Id)
            .Take(take)
            .Select(message => new ChatMessageDto
            {
                MessageId = message.Id,
                Text = message.MessageText,
                UserName = message.Sender.Username,
                Timestamp = message.SentAt
            })
            .ToListAsync();

        messages.Reverse();
        return messages;
    }
}
```

Зарегистрируй сервис в `Program.cs`:

```csharp
builder.Services.AddScoped<IChatMessageService, ChatMessageService>();
```

После этого можно постепенно перевести на него `ChatService` или оставить старый REST
как fallback на время миграции.

---

## 2. Hub

Создай `Hubs/ChatHub.cs`.

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TestingAppWeb.Services.Chat;

namespace TestingAppWeb.Hubs;

[Authorize]
public sealed class ChatHub : Hub
{
    private readonly IChatMessageService _messages;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(IChatMessageService messages, ILogger<ChatHub> logger)
    {
        _messages = messages;
        _logger = logger;
    }

    private int CurrentUserId
    {
        get
        {
            if (int.TryParse(Context.UserIdentifier, out var userId))
            {
                return userId;
            }

            throw new HubException("Не удалось определить пользователя.");
        }
    }

    private bool IsAdmin => Context.User?.IsInRole("Admin") == true;

    private string CurrentUserName =>
        Context.User?.Identity?.Name ?? "unknown";

    public async Task SendMessage(string text)
    {
        var result = await _messages.SendAsync(CurrentUserId, CurrentUserName, text);

        if (result.Status == ChatOperationStatus.Invalid)
        {
            throw new HubException("Сообщение должно быть от 1 до 2000 символов.");
        }

        if (result.Status != ChatOperationStatus.Success || result.Message is null)
        {
            throw new HubException("Не удалось отправить сообщение.");
        }

        // Сначала сообщение уже сохранено в БД, только потом рассылаем.
        await Clients.All.SendAsync("MessageReceived", result.Message);
    }

    public async Task EditMessage(int messageId, string text)
    {
        var result = await _messages.EditAsync(CurrentUserId, IsAdmin, messageId, text);

        switch (result.Status)
        {
            case ChatOperationStatus.Success:
                await Clients.All.SendAsync("MessageEdited", new
                {
                    messageId,
                    text = text.Trim()
                });
                break;

            case ChatOperationStatus.Invalid:
                throw new HubException("Сообщение должно быть от 1 до 2000 символов.");

            case ChatOperationStatus.Forbidden:
                throw new HubException("Можно редактировать только свои сообщения.");

            default:
                throw new HubException("Сообщение не найдено.");
        }
    }

    public async Task DeleteMessage(int messageId)
    {
        var result = await _messages.DeleteAsync(CurrentUserId, IsAdmin, messageId);

        switch (result.Status)
        {
            case ChatOperationStatus.Success:
                await Clients.All.SendAsync("MessageDeleted", messageId);
                break;

            case ChatOperationStatus.Forbidden:
                throw new HubException("Можно удалять только свои сообщения.");

            default:
                throw new HubException("Сообщение не найдено.");
        }
    }

    public Task<IReadOnlyList<TestingAppWeb.Models.Chat.ChatMessageDto>> GetHistory(
        int take = 50,
        int? beforeId = null)
    {
        return _messages.GetHistoryAsync(take, beforeId);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation(
            "User {UserId} connected to chat. ConnectionId={ConnectionId}",
            Context.UserIdentifier,
            Context.ConnectionId);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation(
            "User {UserId} disconnected from chat. ConnectionId={ConnectionId}",
            Context.UserIdentifier,
            Context.ConnectionId);

        await base.OnDisconnectedAsync(exception);
    }
}
```

Почему `Context.UserIdentifier`, а не `userId` из клиента:

- `UserIdentifier` берётся из cookie/claims на сервере;
- клиент не может подставить чужой ID;
- по умолчанию ASP.NET Core использует `ClaimTypes.NameIdentifier`, а он у тебя уже
  выставляется при логине.

Если `UserIdentifier` окажется пустым, добавь свой провайдер:

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

public sealed class NameIdentifierUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        return connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}
```

```csharp
builder.Services.AddSingleton<IUserIdProvider, NameIdentifierUserIdProvider>();
```

---

## 3. Подключение SignalR в Program.cs

Добавь регистрацию:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using TestingAppWeb.Hubs;

builder.Services
    .AddSignalR(options =>
    {
        options.KeepAliveInterval = TimeSpan.FromSeconds(15);
        options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
        options.MaximumReceiveMessageSize = 32 * 1024;
    })
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
```

И endpoint:

```csharp
app.MapHub<ChatHub>("/hubs/chat");
```

`MapHub` ставится после `UseAuthentication()` и `UseAuthorization()`.
`UseWebSockets()` явно добавлять не нужно для `MapHub` в актуальном ASP.NET Core,
но если у тебя появится ошибка `WebSockets is not enabled`, добавь `app.UseWebSockets();`
до `UseRouting()`.

Проверка после этого шага:

```bash
dotnet build
curl -i http://localhost:8081/hubs/chat/negotiate?negotiateVersion=1
```

Без авторизации должен быть `401/302`, с авторизацией — JSON с `connectionToken`.

---

## 4. Боты: очередь вместо polling

Сейчас `ChatBackgroundService` каждые 500 мс дергает `ChatMessageProcessor`, а тот читает
`ConcurrentQueue` из `ChatUserSession`. Для SignalR это лишнее.

Рекомендуемая схема:

```text
ChatHub.SendMessage
        │
        ▼
ChatMessageService.Save
        │
        ├──► Clients.All.SendAsync("MessageReceived")
        │
        ▼
ChatEventQueue (Channel<ChatEvent>)
        │
        ▼
ChatBotBackgroundService
        │
        ├──► боты обрабатывают событие
        ├──► ответ бота сохраняется в БД
        └──► IHubContext<ChatHub>.Clients.All.SendAsync(...)
```

### 4.1. Очередь событий

```csharp
using System.Threading.Channels;

public sealed record ChatEvent(ChatMessage Message, MessageAction Action);

public sealed class ChatEventQueue
{
    private readonly Channel<ChatEvent> _channel =
        Channel.CreateUnbounded<ChatEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    public ValueTask EnqueueAsync(ChatEvent chatEvent, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(chatEvent, cancellationToken);
    }

    public IAsyncEnumerable<ChatEvent> ReadAllAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
```

Регистрация:

```csharp
builder.Services.AddSingleton<ChatEventQueue>();
builder.Services.AddHostedService<ChatBotBackgroundService>();
```

`ChatMessageService` после `SaveChangesAsync()` может сам класть событие в очередь:

```csharp
private readonly ChatEventQueue _events;

// после сохранения:
await _events.EnqueueAsync(new ChatEvent(message, MessageAction.Send));
```

Либо это делает Hub после успешного сохранения. Главное — **после** `SaveChangesAsync`,
чтобы боты не получили событие о сообщении, которого нет в БД.

### 4.2. Фоновый сервис ботов

Не переписывай `ChatBotHandler` целиком. У него уже есть `AcceptNewMessageAsync` и
`TryDequeueOutgoingTask`. Достаточно нового фонового сервиса:

```csharp
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TestingAppWeb.Data;
using TestingAppWeb.Hubs;
using TestingAppWeb.Models.Chat;
using TestingAppWeb.Services.Chat;

public sealed class ChatBotBackgroundService : BackgroundService
{
    private readonly ChatEventQueue _events;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<ChatHub> _hub;
    private readonly ILogger<ChatBotBackgroundService> _logger;

    public ChatBotBackgroundService(
        ChatEventQueue events,
        IServiceScopeFactory scopeFactory,
        IHubContext<ChatHub> hub,
        ILogger<ChatBotBackgroundService> logger)
    {
        _events = events;
        _scopeFactory = scopeFactory;
        _hub = hub;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var chatEvent in _events.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessEventAsync(chatEvent, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bot processing failed for message {MessageId}", chatEvent.Message.Id);
            }
        }
    }

    private async Task ProcessEventAsync(ChatEvent chatEvent, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var botManager = scope.ServiceProvider.GetRequiredService<ChatBotManager>();
        var messages = scope.ServiceProvider.GetRequiredService<IChatMessageService>();

        foreach (var botName in botManager.GetAllBots())
        {
            if (!botManager.TryGetChatBot(botName, out var bot) || !bot.IsWorking)
            {
                continue;
            }

            await bot.AcceptNewMessageAsync((chatEvent.Message, chatEvent.Action));

            while (bot.TryDequeueOutgoingTask(out var botTask))
            {
                await PersistAndBroadcastBotTaskAsync(botTask, messages, cancellationToken);
            }
        }
    }

    private async Task PersistAndBroadcastBotTaskAsync(
        (ChatMessage Message, MessageAction Action) botTask,
        IChatMessageService messages,
        CancellationToken cancellationToken)
    {
        var (message, action) = botTask;

        switch (action)
        {
            case MessageAction.Send:
            {
                var user = await GetUserAsync(message.SenderId, cancellationToken);
                if (user is null)
                {
                    return;
                }

                var result = await messages.SendAsync(user.Id, user.Username, message.MessageText);
                if (result is { Status: ChatOperationStatus.Success, Message: not null })
                {
                    await _hub.Clients.All.SendAsync("MessageReceived", result.Message, cancellationToken);
                }

                break;
            }

            case MessageAction.Edit:
                await _hub.Clients.All.SendAsync(
                    "MessageEdited",
                    new { messageId = message.Id, text = message.MessageText },
                    cancellationToken);
                break;

            case MessageAction.Delete:
                await _hub.Clients.All.SendAsync("MessageDeleted", message.Id, cancellationToken);
                break;
        }
    }

    private async Task<User?> GetUserAsync(int userId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);
    }
}
```

Замечания:

- `MessageAction.Edit`/`Delete` от бота должны применяться к БД, а не только рассылаться.
  В примере выше для краткости показана только рассылка; добавь вызов
  `ChatMessageService.EditAsync`/`DeleteAsync` с `isAdmin: true`, потому что бот
  действует от имени сервера.
- Бот не должен реагировать на сообщения других ботов. В `ChatBotHandler` уже есть
  проверка `user.Role == "Bot"` — не удаляй её.
- Не вызывай `SaveChangesAsync` из `ChatBotBackgroundService` параллельно с Hub
  на одном `DbContext`: у каждого scope свой контекст, это нормально.
- Если ботов несколько и они должны видеть сообщения друг друга, решай это отдельно.
  По умолчанию лучше не зацикливать ботов.

---

## 5. Фронтенд

### 5.1. Подключение клиента

В проекте нет npm-сборки, поэтому проще всего положить клиент локально:

```bash
npm install @microsoft/signalr
```

Скопируй `node_modules/@microsoft/signalr/dist/browser/signalr.min.js` в
`wwwroot/lib/signalr/signalr.min.js`. Так приложение не зависит от CDN.

В `Views/Chat/Index.cshtml` перед модулем:

```html
<script src="~/lib/signalr/signalr.min.js"></script>
<script type="module" src="~/js/chat/main.js" asp-append-version="true"></script>
```

### 5.2. Модуль соединения

Создай `wwwroot/js/chat/signalr.js`:

```javascript
export function createChatConnection(handlers) {
    const connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/chat')
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .configureLogging(signalR.LogLevel.Warning)
        .build();

    connection.on('MessageReceived', handlers.onMessage);
    connection.on('MessageEdited', handlers.onEdit);
    connection.on('MessageDeleted', handlers.onDelete);

    connection.onreconnecting(() => handlers.onStatus?.('reconnecting'));
    connection.onreconnected(() => handlers.onStatus?.('connected'));
    connection.onclose(() => handlers.onStatus?.('disconnected'));

    return connection;
}
```

### 5.3. Новый main.js

Замени polling на соединение:

```javascript
import { setupChatForm } from './form.js';
import { updateMessageList, scrollDownSmooth, isAtBottom } from './messages.js';
import { createChatConnection } from './signalr.js';
import { addEditToContainer } from './edit_message.js';

const form = document.getElementById('chat-form');
const chatContainer = document.querySelector('.chat-container');
const scrollButton = document.getElementById('scroll-down-btn');

const connection = createChatConnection({
    onMessage: (message) => {
        updateMessageList(chatContainer, [[message, 'Send']], false);
    },
    onEdit: ({ messageId, text }) => {
        updateMessageList(chatContainer, [[
            { messageId, text, userName: '', timestamp: new Date().toISOString() },
            'Edit'
        ]], false);
    },
    onDelete: (messageId) => {
        updateMessageList(chatContainer, [[
            { messageId, text: '', userName: '', timestamp: new Date().toISOString() },
            'Delete'
        ]], false);
    },
    onStatus: (status) => console.log('SignalR status:', status)
});

async function start() {
    await connection.start();

    const history = await connection.invoke('GetHistory', 100);
    updateMessageList(
        chatContainer,
        history.map(message => [message, 'Send']),
        true
    );
    scrollDownSmooth(chatContainer);
}

async function sendMessage(text) {
    await connection.invoke('SendMessage', text);
    scrollDownSmooth(chatContainer);
}

await start();

scrollButton.addEventListener('click', () => {
    scrollDownSmooth(chatContainer);
});

setInterval(() => {
    scrollButton.style.display = isAtBottom(chatContainer) ? 'none' : 'inline-block';
}, 700);

setupChatForm(form, chatContainer, sendMessage, scrollDownSmooth);
addEditToContainer(chatContainer);
```

В `edit_message.js` замени вызов REST на:

```javascript
const result = await connection.invoke('EditMessage', messageId, newText);
```

и для удаления:

```javascript
await connection.invoke('DeleteMessage', messageId);
```

Лучше не держать `connection` глобально, а передавать его в `addEditToContainer`
параметром. Пример:

```javascript
addEditToContainer(chatContainer, connection);
```

### 5.4. Важные детали фронтенда

- **Не добавляй сообщение оптимистично**, если сервер рассылает его всем:
  иначе у отправителя будет дубль. Либо жди `MessageReceived`, либо проверяй
  существующий `data-id` перед `appendMessage`.
- **XSS**: в `messages.js` уже используется `textContent`, не заменяй на `innerHTML`.
- **Реконнект**: после `onreconnected` перезагрузи историю, чтобы не потерять
  сообщения, пришедшие во время разрыва.
- **Ошибки**: `HubException` приходит как ошибка `invoke`; покажи пользователю
  текст через `alert` или баннер.
- **CSRF**: SignalR работает с той же cookie; отдельный antiforgery-токен для
  hub-методов не нужен, но `[Authorize]` на хабе обязателен.
- **SameSite**: если откроешь фронтенд с другого origin, cookie не уйдёт.
  Для отдельного frontend-домена нужны CORS + `SameSite=None; Secure`.

---

## 6. Аудит сообщений и миграция

Рекомендую добавить в `ChatMessage`:

```csharp
public DateTimeOffset? EditedAt { get; set; }
public DateTimeOffset? DeletedAt { get; set; }
public bool IsDeleted { get; set; }
```

И индексы в `AppDbContext.OnModelCreating`:

```csharp
modelBuilder.Entity<ChatMessage>()
    .HasIndex(message => message.SentAt);

modelBuilder.Entity<ChatMessage>()
    .HasIndex(message => new { message.SenderId, message.SentAt });
```

Миграция:

```bash
dotnet ef migrations add AddChatMessageAudit
dotnet ef database update
```

Если делаешь soft delete:

- `DeleteAsync` ставит `IsDeleted = true`, `DeletedAt = DateTimeOffset.UtcNow`;
- все запросы истории фильтруют `!message.IsDeleted`;
- клиенту отправляется `MessageDeleted` с ID;
- физическое удаление можно делать отдельным фоновым job'ом.

---

## 7. Тесты

### 7.1. Unit-тесты сервиса

Проверь:

- автор может редактировать своё сообщение;
- другой пользователь получает `Forbidden`;
- администратор может редактировать чужое;
- пустой текст и текст длиннее 2000 символов → `Invalid`;
- удаление чужого сообщения → `Forbidden`.

Для тестов используй PostgreSQL в Testcontainers, а не InMemory: так проверяются
реальные типы `timestamp with time zone` и identity-колонки.

### 7.2. Интеграционные тесты SignalR

Добавь в тестовый проект `Microsoft.AspNetCore.SignalR.Client`.

Схема:

1. Поднять `WebApplicationFactory<Program>` с Testcontainers Postgres.
2. Зарегистрировать пользователя и получить auth-cookie.
3. Подключить `HubConnectionBuilder` с `options.HttpMessageHandlerFactory` /
   `options.Cookies` (или через `WebApplicationFactory`).
4. Подключить два клиента.
5. Отправить сообщение первым, проверить, что оба получили `MessageReceived`.
6. Попробовать редактировать чужое — ожидать `HubException`.
7. Отредактировать своё — оба получили `MessageEdited`.
8. Удалить — оба получили `MessageDeleted`.

Чтобы `WebApplicationFactory<Program>` был доступен, добавь в конец `Program.cs`:

```csharp
public partial class Program { }
```

### 7.3. Ручная проверка

- Два браузера, два разных пользователя.
- Отправить сообщение — появляется у обоих без задержки 500 мс.
- Отредактировать своё — обновляется у обоих.
- Попробовать отредактировать чужое через консоль — ошибка.
- Перезапустить приложение — история на месте, соединение переподключается.
- Отправить сообщение с ботом-триггером — бот отвечает.
- Истечь cookie — hub должен отключиться или запросы начать возвращать 401.

---

## 8. Rollout и удаление старого кода

Не удаляй polling в первый же день. Сделай управляемый переход:

1. Добавь SignalR рядом с polling.
2. Включи его флагом:

```json
"Features": {
  "SignalR": false
}
```

```csharp
if (builder.Configuration.GetValue("Features:SignalR", false))
{
    app.MapHub<ChatHub>("/hubs/chat");
}
```

3. Проверь на dev.
4. Включи флаг по умолчанию.
5. Убедись, что неделю нет ошибок.
6. Удали:

- `ChatUserSession` и `ChatSessionManager`;
- `ChatBackgroundService`;
- пользовательские очереди из `ChatMessageProcessor`;
- `ChatController.GetMessages`, `SendMessage`, `EditMessage`;
- polling-функции из `wwwroot/js/chat/api.js`;
- `setInterval` из `wwwroot/js/chat/main.js`.

`ChatController.Index` оставь — он рендерит страницу.
Историю можно оставить и REST-эндпоинтом, и hub-методом `GetHistory`.

---

## 9. Безопасность и эксплуатация

Чек-лист:

- `[Authorize]` на `ChatHub`.
- `Context.UserIdentifier`, а не `userId` из клиента.
- Проверка владельца при edit/delete; администратор — исключение.
- Валидация длины и trim на сервере.
- Лимит сообщений на пользователя. HTTP rate-limit **не покрывает** кадры WebSocket,
  поэтому сделай отдельный лимитер в hub-методах.
- `MaximumReceiveMessageSize` не должен быть огромным.
- Не логируй пароли, cookie и токены.
- Не отправляй клиенту внутренние тексты исключений — используй `HubException`
  с безопасным сообщением.
- XSS: только `textContent`.
- Масштабирование: один инстанс — можно без backplane. Несколько инстансов —
  Redis backplane (`Microsoft.AspNetCore.SignalR.StackExchangeRedis`).
- За WasmGate (L4/TCP) WebSocket проходит как обычный TCP. Проверь idle timeout
  на стороне WasmGate и keepalive в SignalR.
- TLS: L4 не терминирует TLS. Либо HTTPS на Kestrel, либо HTTP-прокси перед
  приложением. Для cookie с `Secure` нужен HTTPS.
- Мониторинг: добавь метрики `chat_connections_active`, `chat_messages_total{action}`,
  `chat_hub_errors_total`.

---

## 10. Оценка

| Работа | Время |
|---|---|
| Сервис сообщений + Hub | 0.5–1 день |
| Боты через Channel + IHubContext | 0.5–1 день |
| Фронтенд SignalR | 0.5–1 день |
| Аудит/миграция/пагинация | 0.5 дня |
| Тесты и отладка | 0.5–1 день |
| **Итого** | **3–4 дня** |

---

## 11. Частые ошибки

- **Сообщение рассылается до `SaveChangesAsync`** — при падении БД клиенты видят
  сообщение, которого нет в истории.
- **Hub — singleton** — нельзя. Hub создаётся на каждый вызов; DbContext должен
  быть scoped.
- **`Clients.Caller` вместо `Clients.All`** — остальные пользователи не увидят
  сообщение.
- **Дубли у отправителя** — не добавляй сообщение локально, если сервер уже
  рассылает его всем.
- **Polling остался включён** — будут дубли и лишняя нагрузка.
- **Нет проверки владельца** — это тот же IDOR, который мы уже закрыли в REST.
- **Боты реагируют на ботов** — проверяй `Role == "Bot"`.
- **Нет реконнекта** — после разрыва пользователь не увидит новые сообщения.
- **`innerHTML` с текстом сообщения** — XSS.

---

Если хочешь, следующим отдельным этапом можно добавить ClickHouse для аналитики
событий чата: `MessageSent`/`MessageEdited`/`MessageDeleted` писать в очередь,
а из неё — в ClickHouse, и строить отчёты в Grafana. Но это уже после SignalR
и Prometheus/Grafana.
