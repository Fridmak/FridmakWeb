using TestingAppWeb.Models;
using TestingAppWeb.Models.ViewModels;

namespace TestingAppWeb.Interfaces
{
    public interface IFriendshipService
    {
        Task<FriendRequestResult> SendRequestAsync(int requesterId, int addresseeId);
        Task<bool> AcceptRequestAsync(int friendshipId, int currentUserId);
        Task<bool> RejectRequestAsync(int friendshipId, int currentUserId);
        Task<bool> CancelRequestAsync(int friendshipId, int currentUserId);
        Task<bool> RemoveFriendAsync(int friendshipId, int currentUserId);
        Task<IReadOnlyList<FriendListItem>> GetFriendsAsync(int userId);
        Task<IReadOnlyList<FriendRequestItem>> GetIncomingRequestsAsync(int userId);
        Task<IReadOnlyList<FriendRequestItem>> GetOutgoingRequestsAsync(int userId);
        Task<FriendshipInfo?> GetFriendshipAsync(int userId, int otherUserId);
        Task<IReadOnlyList<UserSearchResult>> SearchUsersAsync(int currentUserId, string query, int take = 20);
        Task<int> GetFriendCountAsync(int userId);
    }
}
