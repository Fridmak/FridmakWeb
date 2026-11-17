# SignalR в FridmakWeb

Здесь описано, как в проекте сделаны личные сообщения в реальном времени, какие
шаги были выполнены и какие возможности есть у ASP.NET Core SignalR.

## 1. Что такое SignalR

SignalR — это библиотека ASP.NET Core для двусторонней связи сервера и клиента
поверх постоянного соединения. Сервер может отправлять данные клиенту без
запроса от клиента, а клиент может вызывать методы сервера.

Основные понятия:

- **Hub** — серверный класс, методы которого доступны клиенту.
- **Connection** — одно соединение клиента; у него есть `ConnectionId`.
- **User** — все соединения одного пользователя; определяется `IUserIdProvider`.
- **Group** — именованная группа соединений. В проекте группа = один диалог.
- **Client** — конкретное соединение.
- **Clients.All / Group / User / Caller** — адресаты сообщений.

## 2. Транспорты и согласование

SignalR не всегда использует WebSocket. Клиент и сервер сначала делают
`negotiate`-запрос, а затем выбирают лучший доступный транспорт:

1. **WebSockets** — основной вариант, полный дуплекс.
2. **Server-Sent Events (SSE)** — сервер присылает события, клиент пишет обычными
   HTTP-запросами.
3. **Long Polling** — запасной вариант, если WebSocket/SSE недоступны.

Поэтому SignalR работает и в сетях, где WebSocket запрещён, но с большей
задержкой. В нашем стенде WasmGate — L4/TCP-прокси, поэтому WebSocket проходит
через него как обычный TCP-поток.

## 3. Аутентификация

- Хаб помечен `[Authorize]`; без cookie подключиться нельзя.
- `Context.UserIdentifier` по умолчанию берётся из claim
  `ClaimTypes.NameIdentifier`. У нас там числовой id пользователя.
- Если нужен другой идентификатор, реализуется `IUserIdProvider`.
- Cookie аутентификации отправляется браузером при WebSocket-handshake.
- Для hub-методов не нужен MVC antiforgery-токен, но нужна защита от
  cross-origin подключений и корректные cookie-политики.

## 4. Как это устроено в проекте

### Сервер

Файл: `Hubs/MessagesHub.cs`.

Методы:

- `JoinConversation(conversationId)` — проверяет, что пользователь участник
  диалога, добавляет соединение в группу `conversation:{id}` и отмечает
  сообщения прочитанными.
- `SendMessage(conversationId, text)` — сохраняет сообщение в PostgreSQL и
  рассылает `MessageReceived` всем соединениям группы.
- `MarkRead(conversationId)` — отмечает входящие сообщения прочитанными.
- `LeaveConversation(conversationId)` — убирает соединение из группы.

Рассылка:

```csharp
await Clients.Group($"conversation:{conversationId}")
    .SendAsync("MessageReceived", message);

await Clients.User(otherUserId.ToString())
    .SendAsync("ConversationUpdated", message);
```

`Clients.Group` доставляет сообщение всем открытым вкладкам участников диалога.
`Clients.User` используется для обновления списка диалогов/бейджа у второго
участника.

### Подключение в Program.cs

```csharp
builder.Services.AddSignalR(options =>
{
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    options.MaximumReceiveMessageSize = 32 * 1024;
});

app.MapHub<MessagesHub>("/hubs/messages");
```

### Клиент

- Библиотека лежит локально: `wwwroot/lib/signalr/signalr.min.js`.
- Логика: `wwwroot/js/messages/conversation.js`.
- Подключение:

```js
const connection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/messages')
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .configureLogging(signalR.LogLevel.Warning)
    .build();
```

- После старта клиент вызывает `JoinConversation`.
- При `onreconnected` заново входит в группу и добирает пропущенные сообщения
  через REST `GetMessages`.
- Если WebSocket совсем недоступен, остаётся fallback-поллинг раз в 5 секунд.
- Новые сообщения добавляются в DOM через `textContent`, поэтому XSS через текст
  сообщения не исполняется.

### Метрики

- `fridmak_signalr_connections` — активные соединения.
- `fridmak_direct_messages_total` — отправленные личные сообщения.
- Они видны в `/Admin/Metrics` и в Prometheus.

## 5. Возможности библиотеки SignalR

Кроме базовой отправки сообщений, ASP.NET Core SignalR умеет:

- **Группы и пользователи** — `Groups.AddToGroupAsync`, `Clients.User`,
  `Clients.Group`.
- **Автоматический reconnect на клиенте** — `withAutomaticReconnect`.
- **Сильно типизированные хабы** — `Hub<IClient>` вместо строковых имён методов.
- **`IHubContext<T>`** — отправка сообщений из фоновых сервисов и контроллеров.
- **Клиентские вызовы с результатом** — `InvokeAsync<T>` возвращает значение
  метода хаба.
- **Стриминг** — сервер может отдавать `IAsyncEnumerable<T>`/`ChannelReader<T>`,
  клиент — `stream`.
- **MessagePack** — бинарный протокол вместо JSON
  (`Microsoft.AspNetCore.SignalR.Protocols.MessagePack`).
- **Hub filters** — сквозная логика (логирование, права, аудит) вокруг вызовов.
- **DI** — зависимости хаба регистрируются как обычные scoped-сервисы.
- **Scale-out** — Redis backplane
  (`Microsoft.AspNetCore.SignalR.StackExchangeRedis`) или Azure SignalR Service.
- **Настройки таймаутов** — keepalive, client timeout, размер сообщения,
  максимальное число параллельных вызовов.
- **Пользовательские протоколы** — свой `IHubProtocol`.
- **Клиентские библиотеки** — JavaScript/TypeScript, .NET, Java.

## 6. Ограничения текущей реализации

- SignalR сейчас используется для **личных сообщений**. Общий чат с ботами
  остаётся на polling, потому что боты обрабатываются через отдельную очередь
  `ChatMessageProcessor`.
- Один инстанс приложения. Для нескольких реплик нужен Redis backplane.
- Нет push-уведомлений, когда вкладка закрыта.
- Нет read-receipt UI (сервер уже отправляет `ConversationRead`, но интерфейс
  пока не отображает галочки).

## 7. Проверка

1. Открыть диалог в двух браузерах под разными пользователями.
2. Убедиться, что статус соединения — «онлайн».
3. Отправить сообщение — оно должно появиться у обоих без перезагрузки.
4. Перезапустить приложение/сеть — клиент должен переподключиться.
5. Проверить `GET /Admin/Metrics`: `fridmak_signalr_connections` больше нуля,
   `fridmak_direct_messages_total` растёт.
6. `POST /hubs/messages/negotiate?negotiateVersion=1` должен вернуть `200` и
   список транспортов.

## 8. Безопасность

- `[Authorize]` на хабе.
- Проверка участия в диалоге перед `JoinConversation` и отправкой.
- Нельзя доверять `userId`/`conversationId` из клиента без проверки.
- Ограничение длины сообщения.
- Текст сообщений всегда выводится через `textContent`.
- Cookie `HttpOnly`, `SameSite=Lax`; в облаке — `Secure`.
- CORS не включать без необходимости; при отдельном frontend-домене — явные
  origins и `AllowCredentials`.
