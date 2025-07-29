using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using TestingAppWeb.Bots.ChatBots;
using TestingAppWeb.Data;
using TestingAppWeb.Interfaces;
using TestingAppWeb.Models.Chat;
using TestingAppWeb.Services.Chat;

public class ChatServer
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ChatServer> _logger;
    private readonly ChatHandlerManager _handlerManager;
    private readonly ChatBotsManager _chatBotsManager;

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
        await ProcessTasksInDatabase(context, allTasks);

        DistributeUpdatesToAllHandlers(allTasks);
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

    private void DistributeUpdatesToAllHandlers(List<(int, ChatMSG, MessageAction)> tasks)
    {
        foreach (var (senderId, message, action) in tasks)
        {
            foreach (var userId in _handlerManager.GetAllUserIds())
            {
                _handlerManager.GetOrCreateHandler(userId)
                    .EnqueueIncomingUpdate((message, action));
            }

            SendNewMessageToChatBots((message, action));
        }
    }

    private async void SendNewMessageToChatBots((ChatMSG, MessageAction) task)
    {
        var chatBotNames = _chatBotsManager.GetAllBots().ToArray();

        foreach (var name in chatBotNames)
        {
            if (_chatBotsManager.TryGetChatBot(name, out var bot))
            {
                await bot.AcceptNewMessage(task)
                         .ConfigureAwait(false);
            }
        }
    }

    public async Task RegisterChatBot(string chatBotName, IChatBot chatBot)
    {
        _chatBotsManager.GetOrCreateBot(chatBotName, chatBot);
    }
}