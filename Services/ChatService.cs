using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TestingAppWeb.Data;
using TestingAppWeb.Interfaces;
using TestingAppWeb.Models;
using static System.Collections.Specialized.BitVector32;

namespace TestingAppWeb.Services
{
    public class ChatService : IChatService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ChatService> _logger;
        private readonly int _messagesToShow = 500;
        private readonly ChatHandlerManager _chatHandlerManager;

        public ChatService(AppDbContext context, ILogger<ChatService> logger, ChatHandlerManager handlerManager)
        {
            _context = context;
            _logger = logger;
            _chatHandlerManager = handlerManager;
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
                    .Reverse()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recent messages.");
                return new List<ChatMessageDto>();
            }
        }

        public async Task<bool> EditMessageAsync(EditMessageRequest request)
        {
            var msg = await _context.ChatMessages
                .Include(m => m.Sender)
                .FirstOrDefaultAsync(m => m.Id == request.Id);

            if (msg == null || msg.Sender == null)
                return false;

            var messageHandler = _chatHandlerManager.GetOrCreateHandler(msg.Sender.Id);
            var action = default(Tuple<ChatMSG, MessageAction>);

            if (request.Delete)
            {
                action = Tuple.Create<ChatMSG, MessageAction>(msg, MessageAction.Delete);
            }
            else
            {
                msg.MessageText = request.Message;
                action = Tuple.Create<ChatMSG, MessageAction>(msg, MessageAction.Edit);
            }

            try
            {
                await messageHandler.AddMessage(action);
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

            var messageHandler = _chatHandlerManager.GetOrCreateHandler(msg.Sender.Id);
            var action = Tuple.Create<ChatMSG, MessageAction>(msg, MessageAction.Send);

            try
            {
                await messageHandler.AddMessage(action);
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