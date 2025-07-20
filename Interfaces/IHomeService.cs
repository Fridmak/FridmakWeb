using TestingAppWeb.Models;

namespace TestingAppWeb.Interfaces
{
    public interface IHomeService
    {
        Task<bool> AddFriendRequestAsync(Friend friend);
        Task<IEnumerable<Friend>> GetFriendListAsync();
    }
}
