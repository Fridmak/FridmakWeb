using Microsoft.EntityFrameworkCore;
using TestingAppWeb.Data;
using TestingAppWeb.Models.Chat;
using TestingAppWeb.Services.Observability;

namespace TestingAppWeb.Services.Chat
{
    public interface IChatService
    {
        Task<List<(ChatMessageDto, MessageAction)>> GetMessagesToUpdateAsync(
            int userId,
            bool loadOld = false,
            CancellationToken cancellationToken = default);

        Task<MessageEditResult> EditMessageAsync(
            int userId,
            bool isAdmin,
            EditMessageRequest request,
            CancellationToken cancellationToken = default);

        Task<bool> SendMessageAsync(
            int userId,
            ChatMessageDto messageDto,
            CancellationToken cancellationToken = default);
    }

    public class ChatService : IChatService
    {
        private const int MaxMessageLength = 2000;
        private const int HistoryLimit = 500;

        private readonly AppDbContext _context;
        private readonly ILogger<ChatService> _logger;
        private readonly ChatSessionManager _sessionManager;
        private readonly AppMetrics _metrics;

        public ChatService(
            AppDbContext context,
            ILogger<ChatService> logger,
            ChatSessionManager sessionManager,
            AppMetrics metrics)
        {
            _context = context;
            _logger = logger;
            _sessionManager = sessionManager;
            _metrics = metrics;
        }

        public async Task<List<(ChatMessageDto, MessageAction)>> GetMessagesToUpdateAsync(
            int userId,
            bool loadOld = false,
            CancellationToken cancellationToken = default)
        {
            if (loadOld)
            {
                return await GetRecentMessagesAsync(cancellationToken);
            }

            var session = _sessionManager.GetOrCreateSession(userId);
            var updates = session.DequeueIncomingUpdates().ToList();

            if (updates.Count == 0)
            {
                return new List<(ChatMessageDto, MessageAction)>();
            }

            var senderIds = updates
                .Select(update => update.Message.SenderId)
                .Distinct()
                .ToList();

            var usernames = await _context.Users
                .AsNoTracking()
                .Where(user => senderIds.Contains(user.Id))
                .Select(user => new { user.Id, user.Username })
                .ToDictionaryAsync(
                    user => user.Id,
                    user => user.Username,
                    cancellationToken);

            return updates
                .Select(update => (
                    new ChatMessageDto
                    {
                        MessageId = update.Message.Id,
                        Text = update.Message.MessageText,
                        UserName = usernames.TryGetValue(update.Message.SenderId, out var username)
                            ? username
                            : "unknown",
                        Timestamp = update.Message.SentAt
                    },
                    update.Action
                ))
                .OrderBy(update => update.Item1.Timestamp)
                .ToList();
        }

        public async Task<MessageEditResult> EditMessageAsync(
            int userId,
            bool isAdmin,
            EditMessageRequest request,
            CancellationToken cancellationToken = default)
        {
            var message = await _context.ChatMessages
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken);

            if (message == null)
            {
                return MessageEditResult.NotFound;
            }

            if (message.SenderId != userId && !isAdmin)
            {
                _logger.LogWarning(
                    "User {UserId} attempted to edit message {MessageId} owned by {OwnerId}.",
                    userId,
                    request.Id,
                    message.SenderId);
                return MessageEditResult.Forbidden;
            }

            if (!request.Delete)
            {
                if (string.IsNullOrWhiteSpace(request.Message) ||
                    request.Message.Length > MaxMessageLength)
                {
                    return MessageEditResult.Invalid;
                }
            }

            var messageForQueue = new ChatMessage
            {
                Id = message.Id,
                SenderId = message.SenderId,
                MessageText = request.Delete ? message.MessageText : request.Message.Trim(),
                SentAt = message.SentAt
            };

            var action = request.Delete ? MessageAction.Delete : MessageAction.Edit;

            try
            {
                var session = _sessionManager.GetOrCreateSession(userId);
                session.EnqueueOutgoing((messageForQueue, action));
                _metrics.ChatMessages
                    .WithLabels(action == MessageAction.Delete ? "delete" : "edit")
                    .Inc();
                return MessageEditResult.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing message with ID={Id}.", request.Id);
                return MessageEditResult.Failed;
            }
        }

        public async Task<bool> SendMessageAsync(
            int userId,
            ChatMessageDto messageDto,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(messageDto.Text) ||
                messageDto.Text.Length > MaxMessageLength)
            {
                return false;
            }

            var senderExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(user => user.Id == userId, cancellationToken);

            if (!senderExists)
            {
                return false;
            }

            var message = new ChatMessage
            {
                SentAt = DateTimeOffset.UtcNow,
                SenderId = userId,
                MessageText = messageDto.Text.Trim()
            };

            try
            {
                var session = _sessionManager.GetOrCreateSession(userId);
                session.EnqueueOutgoing((message, MessageAction.Send));
                _metrics.ChatMessages.WithLabels("send").Inc();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message from user {UserId}.", userId);
                return false;
            }
        }

        private async Task<List<(ChatMessageDto, MessageAction)>> GetRecentMessagesAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                var messages = await _context.ChatMessages
                    .AsNoTracking()
                    .Where(message =>
                        !string.IsNullOrEmpty(message.MessageText) &&
                        message.SentAt != default &&
                        !string.IsNullOrEmpty(message.Sender.Username))
                    .OrderByDescending(message => message.SentAt)
                    .Take(HistoryLimit)
                    .Select(message => new ChatMessageDto
                    {
                        Text = message.MessageText,
                        Timestamp = message.SentAt,
                        UserName = message.Sender.Username,
                        MessageId = message.Id
                    })
                    .ToListAsync(cancellationToken);

                // Запрос берёт последние сообщения, а интерфейсу нужен хронологический порядок.
                messages.Reverse();

                return messages
                    .Select(message => (message, MessageAction.Send))
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recent messages.");
                return new List<(ChatMessageDto, MessageAction)>();
            }
        }
    }
}
