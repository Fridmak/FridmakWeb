using System.ComponentModel.DataAnnotations;

namespace TestingAppWeb.Models.Messaging
{
    public class Conversation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int User1Id { get; set; }

        public User User1 { get; set; } = null!;

        [Required]
        public int User2Id { get; set; }

        public User User2 { get; set; } = null!;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset LastMessageAt { get; set; } = DateTimeOffset.UtcNow;

        public ICollection<DirectMessage> Messages { get; set; } = new List<DirectMessage>();
    }

    public class DirectMessage
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ConversationId { get; set; }

        public Conversation Conversation { get; set; } = null!;

        [Required]
        public int SenderId { get; set; }

        public User Sender { get; set; } = null!;

        [Required]
        [StringLength(2000)]
        public string Text { get; set; } = string.Empty;

        public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset? ReadAt { get; set; }
    }
}
