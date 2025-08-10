using TestingAppWeb.Models;

namespace TestingAppWeb.Interfaces
{
    public interface ILoginAcceptable
    {
        Task<User> GetUserByUsernameAsync(string username);
        bool VerifyPassword(string password, string passwordHash);
    }
}
