using TestingAppWeb.Models;

namespace TestingAppWeb.Interfaces
{
    public interface IUserService : IFriendsAcceping, ILoginAcceptable, IRegisterAcceptable, IBotsManager, IProfileOpenable, IProfileEditable
    {
        Task LogoutAsync(string userName);
    }
}
