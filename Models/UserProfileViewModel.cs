using TestingAppWeb.Models;

namespace TestingAppWeb.Models.ViewModels
{
    public class UserProfileViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Role { get; set; }
        public bool IsOwnProfile { get; set; }
        public string Bio { get; set; } = string.Empty;
        public int FriendsCount { get; set; }
        public int? FriendshipId { get; set; }
        public FriendshipStatus? FriendshipStatus { get; set; }
        public bool IsIncomingRequest { get; set; }
    }
}
