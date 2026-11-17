namespace TestingAppWeb.Models.Messaging
{
    public sealed class ConversationDto
    {
        public int Id { get; set; }
        public int OtherUserId { get; set; }
        public string OtherUsername { get; set; } = string.Empty;
        public string OtherBio { get; set; } = string.Empty;
        public string? LastMessageText { get; set; }
        public DateTimeOffset LastMessageAt { get; set; }
        public int UnreadCount { get; set; }
    }

    public sealed class ConversationDetailsDto
    {
        public int Id { get; set; }
        public int OtherUserId { get; set; }
        public string OtherUsername { get; set; } = string.Empty;
        public string OtherBio { get; set; } = string.Empty;
    }

    public sealed class DirectMessageDto
    {
        public int Id { get; set; }
        public int ConversationId { get; set; }
        public int SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public DateTimeOffset SentAt { get; set; }
    }

    public sealed class SendDirectMessageRequest
    {
        public int ConversationId { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    public sealed class MarkConversationReadRequest
    {
        public int ConversationId { get; set; }
    }

    public sealed class ConversationPageViewModel
    {
        public ConversationDetailsDto Conversation { get; set; } = null!;
        public IReadOnlyList<DirectMessageDto> Messages { get; set; } = Array.Empty<DirectMessageDto>();
        public int CurrentUserId { get; set; }
    }
}
