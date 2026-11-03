using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using TestingAppWeb.Bots.ChatBots;
using TestingAppWeb.Data;
using TestingAppWeb.Interfaces;
using TestingAppWeb.Models;
using TestingAppWeb.Models.Chat;
using TestingAppWeb.Services.Chat;
using TestingAppWeb.Services.Chat.Bots;

public class ChatServer
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ChatServer> _logger;
    private readonly ChatHandlerManager _handlerManager;
    private readonly ChatBotsManager _chatBotsManager;
    public readonly int BOTID = -1;

    public ChatServer(
        IServiceProvider serviceProvider,
        ILogger<ChatServer> logger,
        ChatHandlerManager handlerManager,
        ChatBotsManager botsManager)
    {
        _serviceProvider = serviceProvider; 
        _logger = logger;                   
        _handlerManager = handlerManager;
        _chatBotsManager = botsManager;
    }

    public async Task ProcessAllPendingMessages()
    {
        using var scope = _serviceProvider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var allTasks = new List<(int UserId, ChatMSG Message, MessageAction Action)>();

        AddAllPendingMessagesFromPeople(allTasks);
        AddAllPendingMessagesFromBots(allTasks);

        await ProcessTasksInDatabase(context, allTasks);

        await DistributeUpdatesToAllHandlers(allTasks);
    }

    private void AddAllPendingMessagesFromPeople(List<(int UserId, ChatMSG Message, MessageAction Action)> allTasks)
    {
        foreach (var userId in _handlerManager.GetAllUserIds())
        {
            if (_handlerManager.TryGetHandler(userId, out var handler))
            {
                while (handler.TryDequeueOutgoingTask(out var task))
                {
                    allTasks.Add((userId, task.Message, task.Action));
                }
            }
        }
    }

    private void AddAllPendingMessagesFromBots(List<(int UserId, ChatMSG Message, MessageAction Action)> allTasks)
    {
        foreach (var botName in _chatBotsManager.GetAllBots())
        {
            if (_chatBotsManager.TryGetChatBot(botName, out var handler))
            {
                while (handler.TryDequeueOutgoingTask(out var task))
                {
                    allTasks.Add((BOTID, task.Message, task.Action));
                }
            }
        }
    }

    private async Task ProcessTasksInDatabase(AppDbContext context, List<(int, ChatMSG, MessageAction)> tasks)
    {
        foreach (var (userId, message, action) in tasks)
        {
            try
            {
                switch (action)
                {
                    case MessageAction.Send:
                        await context.ChatMessages.AddAsync(message);
                        break;

                    case MessageAction.Edit:
                        var existing = await context.ChatMessages.FindAsync(message.Id);
                        if (existing != null)
                            existing.MessageText = message.MessageText;
                        break;

                    case MessageAction.Delete:
                        var toDelete = await context.ChatMessages.FindAsync(message.Id);
                        if (toDelete != null)
                            context.ChatMessages.Remove(toDelete);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing {Action} for message {MessageId}", action, message.Id);
            }
        }

        await context.SaveChangesAsync();
    }

    private async Task DistributeUpdatesToAllHandlers(List<(int, ChatMSG, MessageAction)> tasks)
    {
        foreach (var (senderId, message, action) in tasks)
        {
            foreach (var userId in _handlerManager.GetAllUserIds())
            {
                _handlerManager.GetOrCreateHandler(userId)
                    .EnqueueIncomingUpdate((message, action));
            }

            foreach (var name in _chatBotsManager.GetAllBots())
            {
                if (_chatBotsManager.TryGetChatBot(name, out var bot))
                {
                    await bot.AcceptNewMessage((message, action))
                             .ConfigureAwait(false);
                }
            }
        }
    }

    public async Task RegisterChatBot(IChatBot chatBot)
    {
        await _chatBotsManager.GetOrCreateBot(chatBot.NAME, chatBot);
    }

    public async Task SetupBots()
    {
        var botServices = _serviceProvider.GetServices<IChatBot>();

        foreach (var bot in botServices)
            await RegisterChatBot(bot);
    }
}