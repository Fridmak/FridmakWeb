using TestingAppWeb.Interfaces;
using TestingAppWeb.Models.Chat;

namespace TestingAppWeb.Services.Chat.Bots
{
    public class HelpBot : IChatBot
    {
        public Task<ChatBotHandle> HandleDeleteSingleMessage(ChatMSG message)
        {
            throw new NotImplementedException();
        }

        public Task<ChatBotHandle> HandleEditSingleMessage(ChatMSG message)
        {
            throw new NotImplementedException();
        }

        public Task<ChatBotHandle> HandleNewSingleMessage(ChatMSG message)
        {
            throw new NotImplementedException();
        }
    }
}
