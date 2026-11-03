using Microsoft.Extensions.Logging;
using TestingAppWeb.Interfaces;
using TestingAppWeb.Models.Chat;

namespace TestingAppWeb.Services.Chat.Bots
{
    public class MemeBot : IChatBot
    {
        public string NAME { get; } = "MemeBot";

        private static readonly ChatBotHandle _nullHandle = new(
            messageText: null,
            botName: "MemeBot",
            Action: MessageAction.None
        );

        private static readonly string[] MemeEmojis = { "🤪", "💀", "🫠", "🚀", "🔥", "🤯", "😭", "🤡", "🎉", "👀", "🫶", "💅", "🍑", "⚡" };
        private static readonly string[] DefaultTriggers = { "скучно", "грустно", "помогите", "фигня", "ничего не понятно", "зато у меня котик", "не хочу", "устал" };

        private readonly Random _random = new();
        private readonly ILogger<MemeBot> _logger;

        private bool _isEnabled = true;
        private readonly HashSet<string> _triggers = new(StringComparer.OrdinalIgnoreCase);

        public MemeBot(ILogger<MemeBot> logger)
        {
            _logger = logger;
            foreach (var trigger in DefaultTriggers)
                _triggers.Add(trigger);
        }

        public async Task<ChatBotHandle> HandleNewSingleMessage(ChatMessageDto message)
        {
            var text = message.Text?.Trim() ?? "";

            if (IsCommand(text))
                return await HandleCommand(message, text);

            if (!_isEnabled)
                return _nullHandle;

            if (_triggers.Any(t => text.Contains(t, StringComparison.OrdinalIgnoreCase)))
            {
                var response = ApplyMemeStyle(text) + " " + GetRandomEmoji();
                return new ChatBotHandle(
                    messageText: response,
                    botName: NAME,
                    Action: MessageAction.Send,
                    toMessage: message
                );
            }

            if (_random.Next(1, 100) <= 15)
            {
                var maybeFunny = ApplyMemeStyle(text);
                if (maybeFunny.Length > 3 && _random.Next(1, 100) <= 50)
                    maybeFunny += " " + GetRandomEmoji();

                return new ChatBotHandle(
                    messageText: maybeFunny,
                    botName: NAME,
                    Action: MessageAction.Edit,
                    toMessage: message
                );
            }

            return _nullHandle;
        }

        public async Task<ChatBotHandle> HandleEditSingleMessage(ChatMessageDto message)
        {
            if (!_isEnabled) return _nullHandle;

            if (_random.Next(1, 100) <= 30)
            {
                var upgraded = ApplyMemeStyle(message.Text) + " " + GetRandomEmoji();
                return new ChatBotHandle(
                    messageText: upgraded,
                    botName: NAME,
                    Action: MessageAction.Edit,
                    toMessage: message
                );
            }

            return _nullHandle;
        }

        public async Task<ChatBotHandle> HandleDeleteSingleMessage(ChatMessageDto message)
        {
            if (!_isEnabled) return _nullHandle;

            if (_random.Next(1, 100) <= 20)
            {
                return new ChatBotHandle(
                    messageText: $"🥲 Было слишком мемно... {GetRandomEmoji()}",
                    botName: NAME,
                    Action: MessageAction.Send,
                    toMessage: message
                );
            }

            return _nullHandle;
        }

        private bool IsCommand(string text) => text.StartsWith("/");

        private async Task<ChatBotHandle> HandleCommand(ChatMessageDto message, string text)
        {
            var command = text.Split(' ')[0].Trim().ToLower();

            return command switch
            {
                "/start" => StartBot(message),
                "/stop" => StopBot(message),
                "/status" => StatusBot(message),
                "/meme" or "/fun" => SendMemeInfo(message),
                "/random" => SendRandomMemeQuote(message),
                "/memeify" => MemeifyMessage(message),
                "/trigger" => await HandleTriggerCommand(message, text),
                _ => UnknownCommand(message)
            };
        }

        private ChatBotHandle StartBot(ChatMessageDto message)
        {
            if (_isEnabled)
                return CreateResponse("Я и так включён! Готов мемизировать! 🔥", message);

            _isEnabled = true;
            _logger.LogInformation("MemeBot включён пользователем {User}", message.UserName);
            return CreateResponse($"🎉 Включён! Теперь всё будет мемно! {GetRandomEmoji()}", message);
        }

        private ChatBotHandle StopBot(ChatMessageDto message)
        {
            if (!_isEnabled)
                return CreateResponse("Я и так выключен... 😴", message);

            _isEnabled = false;
            _logger.LogInformation("MemeBot выключен пользователем {User}", message.UserName);
            return CreateResponse($"💤 Выключён. Но ты всегда можешь вернуть веселье командой /start", message);
        }

        private ChatBotHandle StatusBot(ChatMessageDto message)
        {
            var status = _isEnabled ? "🟢 ВКЛЮЧЁН" : "🔴 ВЫКЛЮЧЕН";
            var count = _triggers.Count;
            return CreateResponse(
                $"🤖 **{NAME}**\n" +
                $"Состояние: {status}\n" +
                $"Триггеров: {count}\n" +
                $"Режим: ${(message.Text.Contains("грустно") ? "спасение от депрессии" : "хаос и мемы")}",
                message);
        }

        private ChatBotHandle SendMemeInfo(ChatMessageDto message)
        {
            return CreateResponse(
                "Я — **MemeBot**! 🚀\n" +
                "• Автомемизирую текст\n" +
                "• Реагирую на грусть и скуку\n" +
                "• Могу быть включён/выключён\n" +
                "• Использую рандомные эмодзи\n\n" +
                "Команды: `/start`, `/stop`, `/status`, `/memeify`, `/random`",
                message);
        }

        private ChatBotHandle SendRandomMemeQuote(ChatMessageDto message)
        {
            var quote = GetRandomQuote();
            var memeQuote = ApplyMemeStyle(quote);
            return CreateResponse(memeQuote + " " + GetRandomEmoji(), message);
        }

        private ChatBotHandle MemeifyMessage(ChatMessageDto message)
        {
            var replyText = message.Text?.Replace("/memeify", "").Trim();
            if (string.IsNullOrWhiteSpace(replyText))
                replyText = "Ты хотел мем? Держи!";

            var result = ApplyMemeStyle(replyText) + " " + GetRandomEmoji();
            return CreateResponse(result, message);
        }

        private async Task<ChatBotHandle> HandleTriggerCommand(ChatMessageDto message, string fullText)
        {
            var parts = fullText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                return CreateResponse("Используй: `/trigger add слово` или `/trigger remove слово`", message);

            var action = parts[1].ToLower();

            if (action == "add" && parts.Length > 2)
            {
                var word = string.Join(" ", parts.Skip(2));
                if (_triggers.Add(word))
                {
                    _logger.LogInformation("Добавлен триггер '{Trigger}' для MemeBot", word);
                    return CreateResponse($"✅ Триггер '{word}' добавлен! Теперь я буду реагировать на него.", message);
                }
                return CreateResponse($"⚠️ Триггер '{word}' уже существует.", message);
            }

            if (action == "remove" && parts.Length > 2)
            {
                var word = string.Join(" ", parts.Skip(2));
                if (_triggers.Remove(word))
                {
                    _logger.LogInformation("Удалён триггер '{Trigger}' для MemeBot", word);
                    return CreateResponse($"🗑️ Триггер '{word}' удалён.", message);
                }
                return CreateResponse($"❌ Триггер '{word}' не найден.", message);
            }

            return CreateResponse("Неизвестное действие. Используй: `/trigger add` или `/trigger remove`", message);
        }

        private ChatBotHandle UnknownCommand(ChatMessageDto message)
        {
            return CreateResponse(
                $"🤷‍♂️ Неизвестная команда. Напиши `/help`, чтобы посмотреть список.\n" +
                $"Или просто скажи *'скучно'* — я сам начну мемить!",
                message);
        }

        private ChatBotHandle CreateResponse(string text, ChatMessageDto replyTo) => new(
            messageText: text,
            botName: NAME,
            Action: MessageAction.Send,
            toMessage: replyTo
        );

        private string ApplyMemeStyle(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            var chars = input.ToLower().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (char.IsLetter(chars[i]) && i % 2 == 0)
                {
                    chars[i] = char.ToUpper(chars[i]);
                }
            }
            return new string(chars);
        }

        private string GetRandomEmoji() => MemeEmojis[_random.Next(MemeEmojis.Length)];

        private string GetRandomQuote()
        {
            var quotes = new[]
            {
                "Программирование — это когда ты 3 часа ищешь ошибку в одной строке",
                "Я не ленивый, я в режиме энергосбережения",
                "Код работает? Не трогай.",
                "Баг — это не ошибка, это скрытая фича",
                "Я не баг, я фича!",
                "Всё, что не сломано — можно сломать. Я попробую.",
                "Если что-то можно интерпретировать неправильно — оно будет интерпретировано неправильно."
            };
            return quotes[_random.Next(quotes.Length)];
        }
    }
}