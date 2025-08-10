using TestingAppWeb.Models;
using TestingAppWeb.Models.Chat;

namespace TestingAppWeb.Interfaces
{
    public interface IChatService
    {
        Task<List<(ChatMessageDto, MessageAction)>> GetMessagesToUpdateAsync(bool loadOld = false);
        Task<bool> EditMessageAsync(EditMessageRequest request);
        Task<bool> SendMessageAsync(ChatMessageDto messageDto, string username);
    }
}
