using TestingAppWeb.Models;

namespace TestingAppWeb.Interfaces
{
    public interface IUserService : ILoginAcceptable, IRegisterAcceptable, IBotsManager, IProfileEditable
    {
        Task LogoutAsync(string userName);
        Task<bool> SetRoleAsync(int userId, string role);
    }
}
