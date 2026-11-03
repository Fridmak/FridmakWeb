using TestingAppWeb.Models;

namespace TestingAppWeb.Interfaces
{
    public interface IRegisterAcceptable
    {
        Task<bool> RegisterUserAsync(RegisterViewModel model);
        Task<bool> RegisterUserAsync(string userName, string email, string password);
        string GetPasswordHash(string password);
    }
}
