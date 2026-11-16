using System.ComponentModel.DataAnnotations;

namespace TestingAppWeb.Models
{
    public enum FriendshipStatus
    {
        Pending = 0,
        Accepted = 1,
        Rejected = 2,
        Blocked = 3
    }

    public enum FriendRequestResult
    {
        Success,
        AlreadyFriends,
        AlreadyPending,
        CannotAddSelf,
        UserNotFound,
        Blocked
    }

    public class Friendship
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int RequesterId { get; set; }

        public User Requester { get; set; } = null!;

        [Required]
        public int AddresseeId { get; set; }

        public User Addressee { get; set; } = null!;

        [Required]
        public FriendshipStatus Status { get; set; } = FriendshipStatus.Pending;

        [Required]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset? RespondedAt { get; set; }
    }
}
