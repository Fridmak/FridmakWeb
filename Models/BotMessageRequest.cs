namespace TestingAppWeb.Models
{
    public class BotMessageRequest
    {
        public string BotName { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public User BotEntity { get; set; }
    }
}
