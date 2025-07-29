using TestingAppWeb.Models.Chat;

namespace TestingAppWeb.Interfaces
{
    public interface IChatChannel
    {
        public Task SendMessageToChat(ChatMessageDto message);
        public Task DeleteMessageFromChat(int messageId);
    }
}