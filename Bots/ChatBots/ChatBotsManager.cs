using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using TestingAppWeb.Interfaces;
using TestingAppWeb.Models;

namespace TestingAppWeb.Bots.ChatBots;

public class ChatBotsManager
{
    private readonly ConcurrentDictionary<string, ChatBotHandler> _bots = new();
    private readonly ConcurrentDictionary<string, User> _botEntities = new();
    private readonly ILogger<ChatBotsManager> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;

    public ChatBotsManager(
        ILogger<ChatBotsManager> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _serviceProvider = serviceProvider;
    }

    public async Task<ChatBotHandler> GetOrCreateBot(string name, IChatBot chatBotService)
    {
        if (_bots.TryGetValue(name, out var existing))
            return existing;

        return await CreateAndRegisterBot(name, chatBotService);
    }

    private async Task<ChatBotHandler> CreateAndRegisterBot(string name, IChatBot chatBotService)
    {
        using var scope = _serviceProvider.CreateScope();
        var userControllerService = scope.ServiceProvider.GetRequiredService<IUserService>();
        var botHandler = scope.ServiceProvider.GetRequiredService<ChatBotHandler>();

        var botEntity = await CreateAndGetBotEntity(name, userControllerService);
        botHandler.Initialize(name, chatBotService, botEntity);

        if (_bots.TryAdd(name, botHandler))
        {
            _botEntities.TryAdd(name, botEntity);
            _logger.LogInformation("ChatBot '{BotName}' created and registered.", name);
            return botHandler;
        }

        return _bots[name];
    }

    public User? GetBotEntityByName(string name)
    {
        if (_botEntities.TryGetValue(name, out var bot))
            return bot;

        _logger.LogError("Could not find bot named: {Name}", name);
        return null;
    }

    public bool TryGetChatBot(string name, out ChatBotHandler bot)
        => _bots.TryGetValue(name, out bot!);

    public bool TryGetChatBotEntity(string name, out User botEntity)
        => _botEntities.TryGetValue(name, out botEntity!);

    public void RemoveChatBot(string name)
    {
        if (_bots.TryRemove(name, out _))
            _logger.LogInformation($"ChatBot {name} removed.");
        else
            _logger.LogWarning($"ChatBot {name} wasn't removed correctly.");
    }

    private async Task<User> CreateAndGetBotEntity(string name, IUserService userControllerService)
    {
        var username = $"[BOT]{name}";

        var existingBot = await userControllerService.GetUserByUsernameAsync(username);
        if (existingBot != null)
        {
            if (existingBot.Role != "Bot")
            {
                await userControllerService.SetRoleAsync(existingBot.Id, "Bot");
            }

            return existingBot;
        }

        var botPassword = _configuration["BotSettings:DefaultPassword"];
        if (string.IsNullOrEmpty(botPassword))
        {
            // Пароль бота не используется для входа. Генерируем его, чтобы не хранить секрет в репозитории.
            botPassword = Guid.NewGuid().ToString("N") + "Aa1!";
            _logger.LogInformation("Bot password is not configured; generated a one-time password for {BotName}.", name);
        }

        var email = $"{username}@gmail.com";

        var registrationSuccess = await userControllerService.RegisterUserAsync(username, email, botPassword);

        if (!registrationSuccess)
        {
            var raceBot = await userControllerService.GetUserByUsernameAsync(username);
            if (raceBot == null)
            {
                throw new InvalidOperationException($"Failed to register bot '{username}' and no existing user found.");
            }

            if (raceBot.Role != "Bot")
            {
                await userControllerService.SetRoleAsync(raceBot.Id, "Bot");
            }

            return raceBot;
        }

        var newBot = await userControllerService.GetUserByUsernameAsync(username);
        if (newBot == null)
        {
            throw new InvalidOperationException($"Bot '{username}' was registered but could not be retrieved.");
        }

        if (newBot.Role != "Bot")
        {
            await userControllerService.SetRoleAsync(newBot.Id, "Bot");
        }

        return newBot;
    }

    public IEnumerable<string> GetAllBots() => _bots.Keys;

    public void TurnBotOff(string name)
    {
        if (!TryGetChatBot(name, out var bot))
        {
            _logger.LogWarning("Cannot turn off unknown bot {BotName}.", name);
            return;
        }

        bot.TurnBotOff();
    }

    public void TurnBotOn(string name)
    {
        if (!TryGetChatBot(name, out var bot))
        {
            _logger.LogWarning("Cannot turn on unknown bot {BotName}.", name);
            return;
        }

        bot.TurnBotOn();
    }

    public bool IsBotOn(string name)
    {
        return TryGetChatBot(name, out var bot) && bot.IsWorking;
    }
}