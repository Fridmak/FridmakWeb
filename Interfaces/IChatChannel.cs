using TestingAppWeb.Models.Chat;

namespace TestingAppWeb.Interfaces
{
    public interface IChatChannel
    {
        public Task<bool> SendMessageToChat(ChatMessageDto message);
        public Task<bool> DeleteMessageFromChat(int messageId);
    }
}