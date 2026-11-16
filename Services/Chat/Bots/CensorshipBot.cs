using TestingAppWeb.Models.Chat;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace TestingAppWeb.Services.Chat.Bots
{
    public class CensorshipBot : IChatBot
    {
        public string Name { get; } = "CensorshipBot";

        private readonly HashSet<string> _badWords = new(StringComparer.OrdinalIgnoreCase);

        private readonly ChatBotHandle _nullHandle = new(
            MessageText: "NoneHandle",
            BotName: "CensorshipBot",
            Action: MessageAction.None);

        public CensorshipBot()
        {
            LoadBadWords();
        }

        private void LoadBadWords()
        {
            var filePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Resources",
                "BannedChatWords.txt");

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Файл с запрещенными словами не найден.", filePath);
            }

            foreach (var word in File.ReadAllLines(filePath))
            {
                if (!string.IsNullOrWhiteSpace(word))
                {
                    _badWords.Add(word.Trim());
                }
            }
        }

        public async Task<ChatBotHandle> HandleNewSingleMessage(ChatMessageDto message)
        {
            if (string.IsNullOrWhiteSpace(message?.Text))
                return _nullHandle;

            var censoredText = CensorText(message.Text);
            if (censoredText != message.Text)
            {
                return new ChatBotHandle(
                    MessageText: censoredText,
                    BotName: Name,
                    Action: MessageAction.Edit,
                    ToMessage: message
                );
            }

            return _nullHandle;
        }

        public async Task<ChatBotHandle> HandleEditSingleMessage(ChatMessageDto message)
        {
            if (string.IsNullOrWhiteSpace(message?.Text))
                return _nullHandle;

            var censoredText = CensorText(message.Text);
            if (censoredText != message.Text)
            {
                return new ChatBotHandle(
                    MessageText: censoredText,
                    BotName: Name,
                    Action: MessageAction.Edit,
                    ToMessage: message
                );
            }

            return _nullHandle;
        }

        public async Task<ChatBotHandle> HandleDeleteSingleMessage(ChatMessageDto message)
        {
            return _nullHandle;
        }

        private string CensorText(string text)
        {
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var censoredWords = words.Select(CensorWord);
            return string.Join(" ", censoredWords);
        }

        private string CensorWord(string word)
        {
            var cleanWord = Regex.Replace(word, @"^\W+|\W+$", "", RegexOptions.None);

            if (string.IsNullOrEmpty(cleanWord))
                return word;

            if (_badWords.Contains(cleanWord))
            {
                if (cleanWord.Length == 1) return "*";
                var censored = cleanWord[0] + new string('*', cleanWord.Length - 2) + cleanWord[cleanWord.Length - 1];

                return word.Replace(cleanWord, censored);
            }

            return word;
        }
    }
}