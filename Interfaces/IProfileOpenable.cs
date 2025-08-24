using TestingAppWeb.Models;

namespace TestingAppWeb.Interfaces
{
    public interface IProfileOpenable
    {
        Task<User> CheckProfileOpen(string userId);
    }
}
