using TestingAppWeb.Bots.ChatBots;

namespace TestingAppWeb.Interfaces
{
    public interface IBotsManager
    {
        Task<ChatBotsManager> GetBotsManager();
    }
}
