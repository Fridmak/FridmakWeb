using System.Collections.Concurrent;
using TestingAppWeb.Interfaces;

namespace TestingAppWeb.Bots.ChatBots;

public class ChatBotsManager
{
    private readonly ConcurrentDictionary<string, ChatBotHandler> _bots = new();
    private readonly ILogger<ChatBotsManager> _logger;
    private readonly ILogger<ChatBotHandler> _botLogger;
    private readonly IChatChannel _chatChannel;

    public ChatBotsManager(
        ILogger<ChatBotsManager> logger,
        IChatChannel chatChannel)
    {
        _logger = logger;
        _chatChannel = chatChannel;
    }

    public ChatBotHandler GetOrCreateBot(string name, IChatBot chatBotService)
    {
        if (_bots.TryGetValue(name, out var existing))
        {
            _logger.LogWarning("ChatBot '{BotName}' already exists. Reusing existing instance.", name);
            return existing;
        }

        var bot = new ChatBotHandler(name, chatBotService, _botLogger);
        if (!_bots.TryAdd(name, bot))
        {
            _logger.LogWarning("Race condition: ChatBot '{BotName}' already added by another thread.", name);
            return _bots[name];
        }

        bot.CreateChatChannel(_chatChannel).ConfigureAwait(false);
        _logger.LogInformation("ChatBot '{BotName}' created and registered.", name);

        return bot;
    }

    public bool TryGetChatBot(string name, out ChatBotHandler bot)
        => _bots.TryGetValue(name, out bot!);

    public void RemoveChatBot(string name)
    {
        if (_bots.TryRemove(name, out _))
        {
            _logger.LogInformation("ChatBot '{BotName}' removed.", name);
        }
    }

    public IEnumerable<string> GetAllBots() => _bots.Keys;
}