using TestingAppWeb.Models;

namespace TestingAppWeb.Interfaces
{
    public interface IUserService
    {
        Task<User> GetUserByUsernameAsync(string username);
        Task<bool> RegisterUserAsync(RegisterViewModel model);
        string GetPasswordHash(string password);
        bool VerifyPassword(string password, string passwordHash);
        Task LogoutAsync(string userName);
        Task<bool> AcceptFriendAsync(int id);
        Task<bool> RejectFriendAsync(int id);
        Task<IEnumerable<Friend>> GetFriendsToApproveAsync();
    }
}
