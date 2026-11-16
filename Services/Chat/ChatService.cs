using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TestingAppWeb.Data;
using TestingAppWeb.Interfaces;
using TestingAppWeb.Models.Chat;

namespace TestingAppWeb.Services.Chat
{
    public class ChatService : IChatService
    {
        private const int MaxMessageLength = 2000;

        private readonly AppDbContext _context;
        private readonly ILogger<ChatService> _logger;
        private readonly int _messagesToShow = 500;
        private readonly ChatHandlerManager _chatHandlerManager;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ChatService(AppDbContext context, ILogger<ChatService> logger, ChatHandlerManager handlerManager, IHttpContextAccessor accessor)
        {
            _context = context;
            _logger = logger;
            _chatHandlerManager = handlerManager;
            _httpContextAccessor = accessor;
        }

        private int GetCurrentUserId()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
                throw new InvalidOperationException("HTTP-контекст недоступен. Вызовите метод в контексте HTTP-запроса.");

            var user = httpContext.User;
            if (user.Identity?.IsAuthenticated != true)
                throw new InvalidOperationException("Пользователь не авторизован.");

            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                throw new InvalidOperationException("Claim 'NameIdentifier' не найден или пуст.");

            if (!int.TryParse(userIdClaim, out int userId))
                throw new InvalidCastException($"Не удалось преобразовать Id пользователя '{userIdClaim}' в число.");

            return userId;
        }

        private bool IsCurrentUserAdmin()
        {
            return _httpContextAccessor.HttpContext?.User?.IsInRole("Admin") == true;
        }

        public async Task<List<(ChatMessageDto, MessageAction)>> GetRecentMessagesAsync()
        {
            try
            {
                var messages = await _context.ChatMessages
                    .AsNoTracking()
                    .Where(m => !string.IsNullOrEmpty(m.MessageText) &&
                                m.SentAt != default &&
                                !string.IsNullOrEmpty(m.Sender.Username))
                    .OrderByDescending(m => m.SentAt)
                    .Take(_messagesToShow)
                    .Select(m => new ChatMessageDto
                    {
                        Text = m.MessageText,
                        Timestamp = m.SentAt,
                        UserName = m.Sender.Username,
                        MessageId = m.Id
                    })
                    .ToListAsync();

                // Запрос берёт последние сообщения, а интерфейсу нужен хронологический порядок.
                messages.Reverse();

                return messages
                    .Select(m => (m, MessageAction.Send))
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recent messages.");
                return new List<(ChatMessageDto, MessageAction)>();
            }
        }

        public async Task<List<(ChatMessageDto, MessageAction)>> GetMessagesToUpdateAsync(bool loadOld = false)
        {
            if (loadOld)
            {
                return await GetRecentMessagesAsync();
            }

            var userId = GetCurrentUserId();
            var handler = _chatHandlerManager.GetOrCreateHandler(userId);
            var updates = handler.GetMessagesToUpdate().ToList();

            if (updates.Count == 0)
            {
                return new List<(ChatMessageDto, MessageAction)>();
            }

            var senderIds = updates
                .Select(m => m.Message.SenderId)
                .Distinct()
                .ToList();

            var usernames = await _context.Users
                .AsNoTracking()
                .Where(u => senderIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Username })
                .ToDictionaryAsync(u => u.Id, u => u.Username);

            var res = updates
                .Select(m => (
                    new ChatMessageDto
                    {
                        MessageId = m.Message.Id,
                        Text = m.Message.MessageText,
                        UserName = usernames.TryGetValue(m.Message.SenderId, out var username)
                            ? username
                            : "unknown",
                        Timestamp = m.Message.SentAt
                    },
                    m.Action
                ))
                .OrderBy(m => m.Item1.Timestamp)
                .ToList();

            return res;
        }

        public async Task<MessageEditResult> EditMessageAsync(EditMessageRequest request)
        {
            var currentUserId = GetCurrentUserId();
            var isAdmin = IsCurrentUserAdmin();

            var msg = await _context.ChatMessages
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == request.Id);

            if (msg == null)
            {
                return MessageEditResult.NotFound;
            }

            if (msg.SenderId != currentUserId && !isAdmin)
            {
                _logger.LogWarning(
                    "User {UserId} attempted to edit message {MessageId} owned by {OwnerId}.",
                    currentUserId,
                    request.Id,
                    msg.SenderId);
                return MessageEditResult.Forbidden;
            }

            if (!request.Delete)
            {
                if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > MaxMessageLength)
                {
                    return MessageEditResult.Invalid;
                }
            }

            var messageForQueue = new ChatMSG
            {
                Id = msg.Id,
                SenderId = msg.SenderId,
                MessageText = request.Delete ? msg.MessageText : request.Message.Trim(),
                SentAt = msg.SentAt
            };

            var action = request.Delete
                ? Tuple.Create(messageForQueue, MessageAction.Delete)
                : Tuple.Create(messageForQueue, MessageAction.Edit);

            try
            {
                var messageHandler = _chatHandlerManager.GetOrCreateHandler(currentUserId);
                messageHandler.AddMessage(action);
                return MessageEditResult.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing message with ID={Id}", request.Id);
                return MessageEditResult.Failed;
            }
        }

        public async Task<bool> SendMessageAsync(ChatMessageDto messageDto)
        {
            if (string.IsNullOrWhiteSpace(messageDto.Text) || messageDto.Text.Length > MaxMessageLength)
            {
                return false;
            }

            var senderId = GetCurrentUserId();

            var senderExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(u => u.Id == senderId);

            if (!senderExists)
            {
                return false;
            }

            var msg = new ChatMSG
            {
                SentAt = DateTimeOffset.UtcNow,
                SenderId = senderId,
                MessageText = messageDto.Text.Trim()
            };

            var action = Tuple.Create(msg, MessageAction.Send);

            try
            {
                var messageHandler = _chatHandlerManager.GetOrCreateHandler(senderId);
                messageHandler.AddMessage(action);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message from user {UserId}", senderId);
                return false;
            }
        }
    }
}
