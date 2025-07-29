using System.Collections.Concurrent;
using TestingAppWeb.Interfaces;
using TestingAppWeb.Models.Chat;

namespace TestingAppWeb.Bots.ChatBots;

public class ChatBot
{
    private readonly ConcurrentDictionary<Guid, TaskItem> _newTasks = new();
    private readonly ConcurrentDictionary<Guid, byte> _failedTasks = new();
    private readonly IChatBot _chatBotService;
    private readonly ILogger<ChatBot> _logger;
    public readonly string Name;

    private IChatChannel? _chatChannel;
    private string _botName = string.Empty;

    public ChatBot(string name, IChatBot chatBotService, ILogger<ChatBot> logger)
    {
        _chatBotService = chatBotService;
        _logger = logger;

        Name = name;
    }

    public async Task CreateChatChannel(IChatChannel chatChannel)
    {
        _chatChannel = chatChannel;
    }

    public async Task AcceptNewMessage((ChatMSG, MessageAction) task)
    {
        var (message, action) = task;
        var item = new TaskItem(message, action);
        _newTasks.TryAdd(Guid.NewGuid(), item);
    }

    public async Task ResolveNewTasks()
    {
        var keys = _newTasks.Keys.ToArray();

        foreach (var key in keys)
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

                var handled = await HandleAction(task);
                if (!IsValidHandle(handled))
                    continue;

                var done = await HandleChatBotDecision(handled);
                if (done)
                {
                    _newTasks.TryRemove(key, out _);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling task {TaskId} from {BotName}", key, _botName);
            }
        }
    }

    private async Task<ChatBotHandle> HandleAction(TaskItem task)
    {
        return task.Action switch
        {
            MessageAction.Send => await _chatBotService.HandleNewSingleMessage(task.Message),
            MessageAction.Delete => await _chatBotService.HandleDeleteSingleMessage(task.Message),
            MessageAction.Edit => await _chatBotService.HandleEditSingleMessage(task.Message),
            _ => new ChatBotHandle()
        };
    }

    private bool IsValidHandle(ChatBotHandle handle) 
    {
        return true;
    }

    private async Task<bool> HandleChatBotDecision(ChatBotHandle handle)
    {
        if (_chatChannel is null)
            return false;

        // Обработка решения бота
        return true;
    }

    private void MarkAsFailed(Guid key)
    {
        _failedTasks.TryAdd(key, 0);
        _newTasks.TryRemove(key, out _);
        _logger.LogWarning("Task {TaskId} marked as failed after 3 attempts", key);
    }

    private sealed record TaskItem(ChatMSG Message, MessageAction Action)
    {
        public int AttemptCount { get; set; }
    }
}