using TestingAppWeb.Models;

namespace TestingAppWeb.Interfaces
{
    public interface ILoginAcceptable
    {
        Task<User> GetUserByUsernameAsync(string username);
        Task<User> GetUserByMailAsync(string email);
        bool VerifyPassword(string password, string passwordHash);
    }
}
