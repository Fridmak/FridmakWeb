using TestingAppWeb.Models;

namespace TestingAppWeb.Interfaces
{
    public interface IUserService : IFriendsAcceping, ILoginAcceptable, IRegisterAcceptable, IBotsManager
    {
        Task LogoutAsync(string userName);
    }
}
