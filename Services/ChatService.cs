using TestingAppWeb.Models;

namespace TestingAppWeb.Services
{
    using global::TestingAppWeb.Data;
    using global::TestingAppWeb.Interfaces;
    using Microsoft.EntityFrameworkCore;

    namespace TestingAppWeb.Services
    {
        public class ChatService : IChatService
        {
            private readonly AppDbContext _context;
            private readonly ILogger<ChatService> _logger;
            private readonly int _messagesToShow = 500;

            public ChatService(AppDbContext context, ILogger<ChatService> logger)
            {
                _context = context;
                _logger = logger;
            }

            public async Task<List<ChatMessageDto>> GetRecentMessagesAsync()
            {
                try
                {
                    return await _context.ChatMessages
                        .Include(m => m.Sender)
                        .Where(m => m.SenderId != null &&
                                    !string.IsNullOrEmpty(m.MessageText) &&
                                    m.SentAt != default(DateTime) &&
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
                        .Reverse()
                        .ToListAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error parsing messages");
                    return new List<ChatMessageDto>();
                }
            }

            public async Task<bool> EditMessageAsync(EditMessageRequest request)
            {
                var msg = await _context.ChatMessages
                    .FirstOrDefaultAsync(m => m.Id == request.Id);

                if (msg == null)
                    return false;

                if (request.Delete)
                {
                    _context.Remove(msg);
                }
                else
                {
                    msg.MessageText = request.Message;
                }

                try
                {
                    await _context.SaveChangesAsync();
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

                _context.ChatMessages.Add(msg);
                await _context.SaveChangesAsync();
                return true;
            }
        }
    }
}
