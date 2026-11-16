using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TestingAppWeb.Data;
using TestingAppWeb.Models.Chat;
using TestingAppWeb.Services.Chat.Bots;

namespace TestingAppWeb.Services.Chat
{
    public class ChatMessageProcessor
    {
        private const int BotUserId = -1;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ChatMessageProcessor> _logger;
        private readonly ChatSessionManager _sessionManager;
        private readonly ChatBotManager _botManager;
        private readonly IReadOnlyList<IChatBot> _bots;

        public ChatMessageProcessor(
            IServiceScopeFactory scopeFactory,
            ILogger<ChatMessageProcessor> logger,
            ChatSessionManager sessionManager,
            ChatBotManager botManager,
            IEnumerable<IChatBot> bots)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _sessionManager = sessionManager;
            _botManager = botManager;
            _bots = bots.ToList();
        }

        public async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var tasks = new List<(int UserId, ChatMessage Message, MessageAction Action)>();

            AddPendingMessagesFromUsers(tasks);
            AddPendingMessagesFromBots(tasks);

            await PersistTasksAsync(context, tasks, cancellationToken);
            await DistributeUpdatesAsync(tasks, cancellationToken);
        }

        public async Task SetupBotsAsync(CancellationToken cancellationToken = default)
        {
            foreach (var bot in _bots)
            {
                await _botManager.GetOrCreateBotAsync(bot.Name, bot);
            }
        }

        private void AddPendingMessagesFromUsers(
            List<(int UserId, ChatMessage Message, MessageAction Action)> tasks)
        {
            foreach (var userId in _sessionManager.GetAllUserIds())
            {
                if (!_sessionManager.TryGetSession(userId, out var session))
                {
                    continue;
                }

                while (session.TryDequeueOutgoing(out var task))
                {
                    tasks.Add((userId, task.Message, task.Action));
                }
            }
        }

        private void AddPendingMessagesFromBots(
            List<(int UserId, ChatMessage Message, MessageAction Action)> tasks)
        {
            foreach (var botName in _botManager.GetAllBots())
            {
                if (!_botManager.TryGetChatBot(botName, out var bot))
                {
                    continue;
                }

                while (bot.TryDequeueOutgoingTask(out var task))
                {
                    tasks.Add((BotUserId, task.Message, task.Action));
                }
            }
        }

        private async Task PersistTasksAsync(
            AppDbContext context,
            List<(int UserId, ChatMessage Message, MessageAction Action)> tasks,
            CancellationToken cancellationToken)
        {
            foreach (var (_, message, action) in tasks)
            {
                try
                {
                    switch (action)
                    {
                        case MessageAction.Send:
                            context.ChatMessages.Add(message);
                            break;

                        case MessageAction.Edit:
                            var existing = await context.ChatMessages
                                .FirstOrDefaultAsync(item => item.Id == message.Id, cancellationToken);

                            if (existing != null)
                            {
                                existing.MessageText = message.MessageText;
                            }

                            break;

                        case MessageAction.Delete:
                            var toDelete = await context.ChatMessages
                                .FirstOrDefaultAsync(item => item.Id == message.Id, cancellationToken);

                            if (toDelete != null)
                            {
                                context.ChatMessages.Remove(toDelete);
                            }

                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error processing {Action} for message {MessageId}.",
                        action,
                        message.Id);
                }
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        private async Task DistributeUpdatesAsync(
            List<(int UserId, ChatMessage Message, MessageAction Action)> tasks,
            CancellationToken cancellationToken)
        {
            foreach (var (_, message, action) in tasks)
            {
                foreach (var userId in _sessionManager.GetAllUserIds())
                {
                    _sessionManager.GetOrCreateSession(userId)
                        .EnqueueIncoming((message, action));
                }

                foreach (var botName in _botManager.GetAllBots())
                {
                    if (_botManager.TryGetChatBot(botName, out var bot))
                    {
                        await bot.AcceptNewMessageAsync((message, action));
                    }
                }
            }
        }
    }
}
