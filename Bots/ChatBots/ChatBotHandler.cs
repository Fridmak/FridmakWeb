using Azure.Core;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using TestingAppWeb.Data;
using TestingAppWeb.Interfaces;
using TestingAppWeb.Models;
using TestingAppWeb.Models.Chat;

namespace TestingAppWeb.Bots.ChatBots;

public class ChatBotHandler
{
    private readonly ConcurrentDictionary<Guid, TaskItem> _newTasks = new();
    private readonly ConcurrentDictionary<Guid, byte> _failedTasks = new();
    private readonly ConcurrentQueue<(ChatMSG Message, MessageAction Action)> _messagesToUpdateToServer = new();
    public bool IsWorking { get; private set; }

    private IChatBot _chatBotService = null!;
    private User _botEntity = null!;
    public string Name = null!;

    private static readonly ChatBotHandle _nullHandle = new(
        Action: MessageAction.None,
        messageText: null,
        botName: null
    );

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ChatBotHandler> _logger;

    public ChatBotHandler(ILogger<ChatBotHandler> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public void Initialize(string name, IChatBot chatBotService, User botEntity)
    {
        if (!string.IsNullOrEmpty(Name))
            throw new InvalidOperationException($"ChatBotHandler for '{Name}' is already initialized.");

        Name = name;
        _chatBotService = chatBotService ?? throw new ArgumentNullException(nameof(chatBotService));
        _botEntity = botEntity ?? throw new ArgumentNullException(nameof(botEntity));
        IsWorking = true;
    }

    public async Task AcceptNewMessage((ChatMSG, MessageAction) task)
    {
        if (!IsWorking)
            return;

        var (message, action) = task;
        var item = new TaskItem { Message = message, Action = action, AttemptCount = 0 };
        var key = Guid.NewGuid();

        if (!_newTasks.TryAdd(key, item))
        {
            _logger.LogWarning("Failed to add task for message {MessageId} in bot {BotName}", message.Id, Name);
            return;
        }

        await ResolveNewTasks();
    }

    public async Task ResolveNewTasks()
    {
        if (!IsWorking)
            return;

        var taskKeys = _newTasks.Keys.ToArray();

        foreach (var key in taskKeys)
        {
            if (_failedTasks.ContainsKey(key) || !_newTasks.TryGetValue(key, out var task))
                continue;

            try
            {
                task.AttemptCount++;

                if (task.AttemptCount > 3)
                {
                    MarkAsFailed(key);
                    continue;
                }

                var handle = await HandleAction(task);
                if (!IsValidHandle(handle))
                    continue;

                var result = await HandleChatBotDecision(handle);
                if (result != null)
                {
                    _newTasks.TryRemove(key, out _);
                    _messagesToUpdateToServer.Enqueue(result.Value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling task {TaskId} from {BotName}", key, Name);
            }
        }
    }

    private async Task<ChatBotHandle> HandleAction(TaskItem task)
    {
        var user = await FindUserAsync(task.Message.SenderId);
        if (user.Role == "Bot")
            return _nullHandle;

        return task.Action switch
        {
            MessageAction.Send => await _chatBotService.HandleNewSingleMessage(await ConvertMessageToDto(task.Message)),
            MessageAction.Delete => await _chatBotService.HandleDeleteSingleMessage(await ConvertMessageToDto(task.Message)),
            MessageAction.Edit => await _chatBotService.HandleEditSingleMessage(await ConvertMessageToDto(task.Message)),
            _ => _nullHandle
        };
    }

    private bool IsValidHandle(ChatBotHandle handle)
    {
        if (handle.Action == MessageAction.None)
            return false;

        if (handle.messageText == null && handle.Action == MessageAction.Send)
            return false;

        if (handle.botName == null)
            return false;

        return true;
    }

    private async Task<(ChatMSG Message, MessageAction Action)?> HandleChatBotDecision(ChatBotHandle handle)
    {
        switch (handle.Action)
        {
            case MessageAction.Send:
                return await HandleChatBotSendDecision(handle);
;
            case MessageAction.Delete:
                return await HandleChatBotDeleteDecision(handle);

            case MessageAction.Edit:
                return await HandleChatBotEditDecision(handle);
            default:
                return (new ChatMSG(), MessageAction.None);

        }
    }

    private async Task<(ChatMSG Message, MessageAction Action)?> HandleChatBotDeleteDecision(ChatBotHandle handle)
    {
        var originalMessage = await FindMessageAsync(handle.toMessage.MessageId);
        var message = new ChatMSG
        {
            SenderId = originalMessage.SenderId,
            SentAt = originalMessage.SentAt,
            Id = originalMessage.Id,
        };

        return (message, MessageAction.Delete);
    }

    private async Task<(ChatMSG Message, MessageAction Action)?> HandleChatBotSendDecision(ChatBotHandle handle)
    {
        var message = new ChatMSG
        {
            MessageText = handle.messageText,
            SenderId = _botEntity.Id,
            SentAt = DateTime.Now
        };

        return (message, MessageAction.Send);
    }

    private async Task<(ChatMSG Message, MessageAction Action)?> HandleChatBotEditDecision(ChatBotHandle handle)
    {
        var originalMessage = await FindMessageAsync(handle.toMessage.MessageId);
        var message = new ChatMSG
        {
            MessageText = handle.messageText,
            SenderId = originalMessage.SenderId,
            SentAt = originalMessage.SentAt,
            Id = handle.toMessage.MessageId
        };

        return (message, MessageAction.Edit);
    }

    private void MarkAsFailed(Guid key)
    {
        if (_newTasks.TryRemove(key, out _))
            _failedTasks.TryAdd(key, 0);
        _logger.LogWarning("Task {TaskId} marked as failed after 3 attempts", key);
    }

    public bool TryDequeueOutgoingTask(out (ChatMSG Message, MessageAction Action) task)
    {
        return _messagesToUpdateToServer.TryDequeue(out task);
    }

    private async Task<ChatMessageDto> ConvertMessageToDto(ChatMSG message)
    {
        var user = await FindUserAsync(message.SenderId);

        return new ChatMessageDto
        {
            Text = message.MessageText,
            UserName = user.Username,
            Timestamp = message.SentAt,
            MessageId = message.Id
        };
    }

    private async Task<User> FindUserAsync(int userId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        return user;
    }

    private async Task<ChatMSG>? FindMessageAsync(int messageId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var msg = await context.ChatMessages
                .Include(m => m.Sender)
                .FirstOrDefaultAsync(m => m.Id == messageId);

        return msg;
    }

    public void TurnBotOn()
    {
        IsWorking = true;
    }

    public void TurnBotOff()
    {
        IsWorking = false;
    }

    private class TaskItem
    {
        public ChatMSG Message { get; set; } = null!;
        public MessageAction Action { get; set; }
        public int AttemptCount { get; set; }
    }
}