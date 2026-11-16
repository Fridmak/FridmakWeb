using TestingAppWeb.Models.Chat;

namespace TestingAppWeb.Services.Chat.Bots
{
    public class HelpBot : IChatBot
    {
        public string Name { get; } = "HelpBot";
        private readonly ChatBotHandle _nullHandle = new ChatBotHandle(
                MessageText: "NoneHandle",
                BotName: "HelpBot",
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
                MessageText: $"{message.Text}: Viewed",
                BotName: Name,
                Action: MessageAction.Edit,
                ToMessage: message);


            return handle;
        }
    }
}
