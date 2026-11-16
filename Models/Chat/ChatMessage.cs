using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TestingAppWeb.Models.Chat
{
    [Table("ChatMessages")]
    public class ChatMessage
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int SenderId { get; set; }

        [Required]
        public User Sender { get; set; } = null!;

        [Required]
        public string MessageText { get; set; } = null!;

        [Required]
        public DateTimeOffset SentAt { get; set; }
    }
}
