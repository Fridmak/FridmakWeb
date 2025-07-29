using System.ComponentModel.DataAnnotations;

namespace TestingAppWeb.Models.Chat
{
    public class ChatMSG
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public int SenderId { get; set; }
        [Required]
        public User Sender { get; set; }
        [Required]
        public string MessageText { get; set; }
        [Required]
        public DateTime SentAt { get; set; }
    }
}
