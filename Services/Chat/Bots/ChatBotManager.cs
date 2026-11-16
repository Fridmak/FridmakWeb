using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using TestingAppWeb.Models;
using TestingAppWeb.Options;
using TestingAppWeb.Services.Users;

namespace TestingAppWeb.Services.Chat.Bots
{
    public class ChatBotManager
    {
        private readonly ConcurrentDictionary<string, ChatBotHandler> _bots = new();
        private readonly ConcurrentDictionary<string, User> _botEntities = new();
        private readonly ILogger<ChatBotManager> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly BotOptions _botOptions;

        public ChatBotManager(
            ILogger<ChatBotManager> logger,
            IServiceScopeFactory scopeFactory,
            IOptions<BotOptions> botOptions)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _botOptions = botOptions.Value;
        }

        public async Task<ChatBotHandler> GetOrCreateBotAsync(string name, IChatBot chatBotService)
        {
            if (_bots.TryGetValue(name, out var existing))
            {
                return existing;
            }

            return await CreateAndRegisterBotAsync(name, chatBotService);
        }

        public bool TryGetChatBot(string name, out ChatBotHandler bot)
        {
            return _bots.TryGetValue(name, out bot!);
        }

        public bool TryGetChatBotEntity(string name, out User botEntity)
        {
            return _botEntities.TryGetValue(name, out botEntity!);
        }

        public User? GetBotEntityByName(string name)
        {
            if (_botEntities.TryGetValue(name, out var bot))
            {
                return bot;
            }

            _logger.LogError("Could not find bot named {BotName}.", name);
            return null;
        }

        public void RemoveChatBot(string name)
        {
            if (_bots.TryRemove(name, out _))
            {
                _logger.LogInformation("Chat bot {BotName} removed.", name);
            }
            else
            {
                _logger.LogWarning("Chat bot {BotName} was not removed correctly.", name);
            }
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

        private async Task<ChatBotHandler> CreateAndRegisterBotAsync(
            string name,
            IChatBot chatBotService)
        {
            using var scope = _scopeFactory.CreateScope();
            var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
            var botHandler = scope.ServiceProvider.GetRequiredService<ChatBotHandler>();

            var botEntity = await CreateAndGetBotEntityAsync(name, userService);
            botHandler.Initialize(name, chatBotService, botEntity);

            if (_bots.TryAdd(name, botHandler))
            {
                _botEntities.TryAdd(name, botEntity);
                _logger.LogInformation("Chat bot {BotName} created and registered.", name);
                return botHandler;
            }

            return _bots[name];
        }

        private async Task<User> CreateAndGetBotEntityAsync(
            string name,
            IUserService userService)
        {
            var username = $"[BOT]{name}";

            var existingBot = await userService.GetUserByUsernameAsync(username);
            if (existingBot != null)
            {
                await EnsureBotRoleAsync(existingBot, userService);
                return existingBot;
            }

            var botPassword = _botOptions.DefaultPassword;
            if (string.IsNullOrEmpty(botPassword))
            {
                // Пароль бота не используется для входа. Генерируем его, чтобы не хранить секрет в репозитории.
                botPassword = Guid.NewGuid().ToString("N") + "Aa1!";
                _logger.LogInformation(
                    "Bot password is not configured; generated a one-time password for {BotName}.",
                    name);
            }

            var email = $"{username}@gmail.com";
            var registrationSuccess = await userService.RegisterUserAsync(username, email, botPassword);

            if (!registrationSuccess)
            {
                var raceBot = await userService.GetUserByUsernameAsync(username);
                if (raceBot == null)
                {
                    throw new InvalidOperationException(
                        $"Failed to register bot '{username}' and no existing user found.");
                }

                await EnsureBotRoleAsync(raceBot, userService);
                return raceBot;
            }

            var newBot = await userService.GetUserByUsernameAsync(username);
            if (newBot == null)
            {
                throw new InvalidOperationException(
                    $"Bot '{username}' was registered but could not be retrieved.");
            }

            await EnsureBotRoleAsync(newBot, userService);
            return newBot;
        }

        private static async Task EnsureBotRoleAsync(User bot, IUserService userService)
        {
            if (bot.Role != "Bot")
            {
                await userService.SetRoleAsync(bot.Id, "Bot");
            }
        }
    }
}
