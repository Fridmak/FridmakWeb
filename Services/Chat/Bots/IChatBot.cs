using TestingAppWeb.Models.Chat;

namespace TestingAppWeb.Services.Chat.Bots
{
    public interface IChatBot
    {
        string Name { get; }

        Task<ChatBotHandle> HandleNewSingleMessage(ChatMessageDto message);

        Task<ChatBotHandle> HandleDeleteSingleMessage(ChatMessageDto message);

        Task<ChatBotHandle> HandleEditSingleMessage(ChatMessageDto message);
    }
}
