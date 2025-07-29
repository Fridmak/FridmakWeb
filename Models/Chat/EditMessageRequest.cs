namespace TestingAppWeb.Models.Chat
{
    public class EditMessageRequest
    {
        public int Id { get; set; }

        public string Message { get; set; }

        public string Comment { get; set; }

        public bool Delete {  get; set; }
    }
}
