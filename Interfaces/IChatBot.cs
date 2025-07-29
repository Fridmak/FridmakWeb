using System.Collections.Concurrent;
using TestingAppWeb.Models.Chat;

namespace TestingAppWeb.Interfaces
{
    public interface IChatBot
    {
        public Task<ChatBotHandle> HandleNewSingleMessage(ChatMSG message);
        public Task<ChatBotHandle> HandleDeleteSingleMessage(ChatMSG message);
        public Task<ChatBotHandle> HandleEditSingleMessage(ChatMSG message);
    }
}
