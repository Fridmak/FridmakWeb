namespace TestingAppWeb.Models.Chat
{
    public sealed record ChatBotHandle(
        MessageAction Action,
        string? MessageText,
        string? BotName,
        ChatMessageDto? ToMessage = null);
}
