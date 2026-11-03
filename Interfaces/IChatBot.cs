using System.Collections.Concurrent;
using TestingAppWeb.Models.Chat;

namespace TestingAppWeb.Interfaces
{
    public interface IChatBot
    {
        string NAME { get; }
        public Task<ChatBotHandle> HandleNewSingleMessage(ChatMessageDto message);
        public Task<ChatBotHandle> HandleDeleteSingleMessage(ChatMessageDto message);
        public Task<ChatBotHandle> HandleEditSingleMessage(ChatMessageDto message);
    }
}
