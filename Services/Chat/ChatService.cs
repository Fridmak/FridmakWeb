using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TestingAppWeb.Data;
using TestingAppWeb.Interfaces;
using TestingAppWeb.Models;

namespace TestingAppWeb.Services.Chat
{
    public class ChatService : IChatService
    {
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
            if (!user.Identity.IsAuthenticated)
                throw new InvalidOperationException("Пользователь не авторизован.");

            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                throw new InvalidOperationException("Claim 'NameIdentifier' не найден или пуст.");

            if (!int.TryParse(userIdClaim, out int userId))
                throw new InvalidCastException($"Не удалось преобразовать Id пользователя '{userIdClaim}' в число.");

            return userId;
        }

        public async Task<List<(ChatMessageDto, MessageAction)>> GetRecentMessagesAsync()
        {
            try
            {
                var messages = await _context.ChatMessages
                    .Include(m => m.Sender)
                    .Where(m => m.SenderId != null &&
                                !string.IsNullOrEmpty(m.MessageText) &&
                                m.SentAt != default &&
                                m.Sender != null &&
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

                var result = messages
                    .OrderBy(m => m.Timestamp)
                    .Select(m => (m, MessageAction.Send))
                    .ToList();

                return result;
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
            var updates = handler.GetMessagesToUpdate();

            return updates.Select(m => (
                new ChatMessageDto
                {
                    MessageId = m.Message.Id,
                    Text = m.Message.MessageText,
                    UserName = m.Message.Sender?.Username ?? "Unknown",
                    Timestamp = m.Message.SentAt
                },
                m.Action
            )).ToList();
        }

        public async Task<bool> EditMessageAsync(EditMessageRequest request)
        {
            var msg = await _context.ChatMessages
                .Include(m => m.Sender)
                .FirstOrDefaultAsync(m => m.Id == request.Id);

            if (msg == null || msg.Sender == null)
                return false;

            var messageHandler = _chatHandlerManager.GetOrCreateHandler(GetCurrentUserId());
            var action = default(Tuple<ChatMSG, MessageAction>);

            if (request.Delete)
            {
                action = Tuple.Create(msg, MessageAction.Delete);
            }
            else
            {
                msg.MessageText = request.Message;
                action = Tuple.Create(msg, MessageAction.Edit);
            }

            try
            {
                messageHandler.AddMessage(action);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing message with ID={Id}", request.Id);
                return false;
            }
        }

        public async Task<bool> SendMessageAsync(ChatMessageDto messageDto, string username)
        {
            var sender = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == username);

            if (sender == null)
                return false;

            var msg = new ChatMSG
            {
                SentAt = DateTime.UtcNow,
                SenderId = sender.Id,
                MessageText = messageDto.Text
            };

            var messageHandler = _chatHandlerManager.GetOrCreateHandler(GetCurrentUserId());
            var action = Tuple.Create(msg, MessageAction.Send);

            try
            {
                messageHandler.AddMessage(action);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message from user '{Username}'", username);
                return false;
            }
        }
    }
}