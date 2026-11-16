namespace TestingAppWeb.Models.Chat
{
    public class ChatMessageDto
    {
        public string Text { get; set; } = string.Empty;
        public DateTimeOffset Timestamp { get; set; }
        public string UserName { get; set; } = string.Empty;

        public int MessageId { get; set; }
    }
}
