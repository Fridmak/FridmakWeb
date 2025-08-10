using TestingAppWeb.Models;

namespace TestingAppWeb.Interfaces
{
    public interface IFriendsAcceping
    {
        Task<bool> AcceptFriendAsync(int id);
        Task<bool> RejectFriendAsync(int id);
        Task<IEnumerable<Friend>> GetFriendsToApproveAsync();
    }
}
