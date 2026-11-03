namespace TestingAppWeb.Models.Chat
{
    public record ChatBotHandle(
        MessageAction Action,
        string? messageText,
        string? botName,
        ChatMessageDto? toMessage = null
    );
}
