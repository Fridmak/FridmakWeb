using TestingAppWeb.Models;

namespace TestingAppWeb.Models.ViewModels
{
    public class FriendListItem
    {
        public int FriendshipId { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = null!;
        public string Bio { get; set; } = string.Empty;
        public DateTimeOffset FriendsSince { get; set; }
    }

    public class FriendRequestItem
    {
        public int FriendshipId { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = null!;
        public string Bio { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
    }

    public class FriendsPageViewModel
    {
        public IReadOnlyList<FriendListItem> Friends { get; set; } = Array.Empty<FriendListItem>();
        public IReadOnlyList<FriendRequestItem> IncomingRequests { get; set; } = Array.Empty<FriendRequestItem>();
        public IReadOnlyList<FriendRequestItem> OutgoingRequests { get; set; } = Array.Empty<FriendRequestItem>();
    }

    public class UserSearchResult
    {
        public int UserId { get; set; }
        public string Username { get; set; } = null!;
        public string Bio { get; set; } = string.Empty;
        public FriendshipStatus? Status { get; set; }
        public int? FriendshipId { get; set; }
        public bool IsIncomingRequest { get; set; }
    }

    public class AddFriendViewModel
    {
        public string? Query { get; set; }
        public IReadOnlyList<UserSearchResult> Results { get; set; } = Array.Empty<UserSearchResult>();
    }

    public class FriendshipInfo
    {
        public int FriendshipId { get; set; }
        public FriendshipStatus Status { get; set; }
        public bool IsIncomingRequest { get; set; }
    }
}
