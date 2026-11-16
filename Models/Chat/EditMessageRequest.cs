namespace TestingAppWeb.Models.Chat
{
    public class EditMessageRequest
    {
        public int Id { get; set; }

        public string Message { get; set; } = string.Empty;

        public string Comment { get; set; } = string.Empty;

        public bool Delete {  get; set; }
    }
}
