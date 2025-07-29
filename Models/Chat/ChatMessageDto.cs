namespace TestingAppWeb.Models.Chat
{
    public class ChatMessageDto
    {
        public string Text { get; set; }
        public DateTime Timestamp { get; set; }
        public string UserName { get; set; }

        public int MessageId { get; set; }
    }
}
