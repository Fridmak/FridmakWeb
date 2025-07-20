using TestingAppWeb.Models;

namespace TestingAppWeb.Interfaces
{
    public interface IChatService
    {
        Task<List<ChatMessageDto>> GetRecentMessagesAsync();
        Task<bool> EditMessageAsync(EditMessageRequest request);
        Task<bool> SendMessageAsync(ChatMessageDto messageDto, string username);
    }
}
