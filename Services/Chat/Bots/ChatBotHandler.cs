using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using TestingAppWeb.Data;
using TestingAppWeb.Models;
using TestingAppWeb.Models.Chat;

namespace TestingAppWeb.Services.Chat.Bots
{
    public class ChatBotHandler
    {
        private readonly ConcurrentDictionary<Guid, TaskItem> _newTasks = new();
        private readonly ConcurrentDictionary<Guid, byte> _failedTasks = new();
        private readonly ConcurrentQueue<(ChatMessage Message, MessageAction Action)> _messagesToUpdateToServer = new();
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ChatBotHandler> _logger;

        private IChatBot _chatBotService = null!;
        private User _botEntity = null!;

        private static readonly ChatBotHandle NullHandle = new(
            Action: MessageAction.None,
            MessageText: null,
            BotName: null);

        public ChatBotHandler(
            ILogger<ChatBotHandler> logger,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        public bool IsWorking { get; private set; }

        public string BotName { get; private set; } = null!;

        public void Initialize(string name, IChatBot chatBotService, User botEntity)
        {
            if (!string.IsNullOrEmpty(BotName))
            {
                throw new InvalidOperationException($"Chat bot handler for '{BotName}' is already initialized.");
            }

            BotName = name;
            _chatBotService = chatBotService ?? throw new ArgumentNullException(nameof(chatBotService));
            _botEntity = botEntity ?? throw new ArgumentNullException(nameof(botEntity));
            IsWorking = true;
        }

        public async Task AcceptNewMessageAsync((ChatMessage Message, MessageAction Action) task)
        {
            if (!IsWorking)
            {
                return;
            }

            var (message, action) = task;
            var item = new TaskItem
            {
                Message = message,
                Action = action,
                AttemptCount = 0
            };

            var key = Guid.NewGuid();

            if (!_newTasks.TryAdd(key, item))
            {
                _logger.LogWarning(
                    "Failed to add a task for message {MessageId} in bot {BotName}.",
                    message.Id,
                    BotName);
                return;
            }

            await ResolveNewTasksAsync();
        }

        public bool TryDequeueOutgoingTask(out (ChatMessage Message, MessageAction Action) task)
        {
            return _messagesToUpdateToServer.TryDequeue(out task);
        }

        public void TurnBotOn()
        {
            IsWorking = true;
        }

        public void TurnBotOff()
        {
            IsWorking = false;
        }

        private async Task ResolveNewTasksAsync()
        {
            if (!IsWorking)
            {
                return;
            }

            var taskKeys = _newTasks.Keys.ToArray();

            foreach (var key in taskKeys)
            {
                if (_failedTasks.ContainsKey(key) || !_newTasks.TryGetValue(key, out var task))
                {
                    continue;
                }

                try
                {
                    task.AttemptCount++;

                    if (task.AttemptCount > 3)
                    {
                        MarkAsFailed(key);
                        continue;
                    }

                    var handle = await HandleActionAsync(task);
                    if (!IsValidHandle(handle))
                    {
                        continue;
                    }

                    var result = await HandleChatBotDecisionAsync(handle);
                    if (result != null)
                    {
                        _newTasks.TryRemove(key, out _);
                        _messagesToUpdateToServer.Enqueue(result.Value);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error handling task {TaskId} from bot {BotName}.",
                        key,
                        BotName);
                }
            }
        }

        private async Task<ChatBotHandle> HandleActionAsync(TaskItem task)
        {
            var user = await FindUserAsync(task.Message.SenderId);
            if (user == null || user.Role == "Bot")
            {
                return NullHandle;
            }

            var messageDto = await ConvertMessageToDtoAsync(task.Message);

            return task.Action switch
            {
                MessageAction.Send => await _chatBotService.HandleNewSingleMessage(messageDto),
                MessageAction.Delete => await _chatBotService.HandleDeleteSingleMessage(messageDto),
                MessageAction.Edit => await _chatBotService.HandleEditSingleMessage(messageDto),
                _ => NullHandle
            };
        }

        private static bool IsValidHandle(ChatBotHandle handle)
        {
            if (handle.Action == MessageAction.None || handle.BotName == null)
            {
                return false;
            }

            if (handle.Action != MessageAction.Delete && handle.MessageText == null)
            {
                return false;
            }

            if (handle.Action is MessageAction.Edit or MessageAction.Delete &&
                handle.ToMessage == null)
            {
                return false;
            }

            return true;
        }

        private async Task<(ChatMessage Message, MessageAction Action)?> HandleChatBotDecisionAsync(
            ChatBotHandle handle)
        {
            return handle.Action switch
            {
                MessageAction.Send => await HandleChatBotSendDecisionAsync(handle),
                MessageAction.Delete => await HandleChatBotDeleteDecisionAsync(handle),
                MessageAction.Edit => await HandleChatBotEditDecisionAsync(handle),
                _ => null
            };
        }

        private async Task<(ChatMessage Message, MessageAction Action)?> HandleChatBotDeleteDecisionAsync(
            ChatBotHandle handle)
        {
            var originalMessage = await FindMessageAsync(handle.ToMessage!.MessageId);
            if (originalMessage == null)
            {
                return null;
            }

            var message = new ChatMessage
            {
                SenderId = originalMessage.SenderId,
                SentAt = originalMessage.SentAt,
                Id = originalMessage.Id
            };

            return (message, MessageAction.Delete);
        }

        private Task<(ChatMessage Message, MessageAction Action)?> HandleChatBotSendDecisionAsync(
            ChatBotHandle handle)
        {
            var message = new ChatMessage
            {
                MessageText = handle.MessageText!,
                SenderId = _botEntity.Id,
                SentAt = DateTimeOffset.UtcNow
            };

            return Task.FromResult<(ChatMessage, MessageAction)?>((message, MessageAction.Send));
        }

        private async Task<(ChatMessage Message, MessageAction Action)?> HandleChatBotEditDecisionAsync(
            ChatBotHandle handle)
        {
            var originalMessage = await FindMessageAsync(handle.ToMessage!.MessageId);
            if (originalMessage == null)
            {
                return null;
            }

            var message = new ChatMessage
            {
                MessageText = handle.MessageText!,
                SenderId = originalMessage.SenderId,
                SentAt = originalMessage.SentAt,
                Id = handle.ToMessage.MessageId
            };

            return (message, MessageAction.Edit);
        }

        private void MarkAsFailed(Guid key)
        {
            if (_newTasks.TryRemove(key, out _))
            {
                _failedTasks.TryAdd(key, 0);
            }

            _logger.LogWarning("Task {TaskId} marked as failed after 3 attempts.", key);
        }

        private async Task<ChatMessageDto> ConvertMessageToDtoAsync(ChatMessage message)
        {
            var user = await FindUserAsync(message.SenderId);

            return new ChatMessageDto
            {
                Text = message.MessageText,
                UserName = user?.Username ?? "unknown",
                Timestamp = message.SentAt,
                MessageId = message.Id
            };
        }

        private async Task<User?> FindUserAsync(int userId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            return await context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(user => user.Id == userId);
        }

        private async Task<ChatMessage?> FindMessageAsync(int messageId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            return await context.ChatMessages
                .Include(message => message.Sender)
                .FirstOrDefaultAsync(message => message.Id == messageId);
        }

        private class TaskItem
        {
            public ChatMessage Message { get; set; } = null!;
            public MessageAction Action { get; set; }
            public int AttemptCount { get; set; }
        }
    }
}
