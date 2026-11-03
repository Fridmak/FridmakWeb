using TestingAppWeb.Interfaces;
using TestingAppWeb.Models.Chat;

namespace TestingAppWeb.Services.Chat.Bots
{
    public class HelpBot : IChatBot
    {
        public string NAME { get; } = "HelpBot";
        private readonly ChatBotHandle _nullHandle = new ChatBotHandle(
                messageText: "NoneHandle",
                botName: "HelpBot",
                Action: MessageAction.None);

        public async Task<ChatBotHandle> HandleDeleteSingleMessage(ChatMessageDto message)
        {
            return _nullHandle;
        }

        public async Task<ChatBotHandle> HandleEditSingleMessage(ChatMessageDto message)
        {
            return _nullHandle;
        }

        public async Task<ChatBotHandle> HandleNewSingleMessage(ChatMessageDto message)
        {
            var handle = new ChatBotHandle(
                messageText: $"{message.Text}: Viewed",
                botName: NAME,
                Action: MessageAction.Edit,
                toMessage: message);


            return handle;
        }
    }
}
