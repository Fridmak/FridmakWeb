using Microsoft.EntityFrameworkCore;
using TestingAppWeb.Data;
using TestingAppWeb.Models;
using TestingAppWeb.Models.Messaging;
using TestingAppWeb.Services.Observability;

namespace TestingAppWeb.Services.Messaging
{
    public interface IConversationService
    {
        Task<ConversationDetailsDto?> GetOrCreateAsync(
            int currentUserId,
            int otherUserId,
            CancellationToken cancellationToken = default);

        Task<ConversationDetailsDto?> GetDetailsAsync(
            int conversationId,
            int currentUserId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ConversationDto>> GetConversationsAsync(
            int userId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<DirectMessageDto>> GetMessagesAsync(
            int conversationId,
            int userId,
            int? afterId = null,
            CancellationToken cancellationToken = default);

        Task<DirectMessageDto?> SendMessageAsync(
            int conversationId,
            int userId,
            string senderName,
            string text,
            CancellationToken cancellationToken = default);

        Task<bool> MarkReadAsync(
            int conversationId,
            int userId,
            CancellationToken cancellationToken = default);
    }

    public class ConversationService : IConversationService
    {
        private const int MessagePageSize = 100;
        private const int MaxMessageLength = 2000;

        private readonly AppDbContext _context;
        private readonly ILogger<ConversationService> _logger;
        private readonly AppMetrics _metrics;

        public ConversationService(
            AppDbContext context,
            ILogger<ConversationService> logger,
            AppMetrics metrics)
        {
            _context = context;
            _logger = logger;
            _metrics = metrics;
        }

        public async Task<ConversationDetailsDto?> GetOrCreateAsync(
            int currentUserId,
            int otherUserId,
            CancellationToken cancellationToken = default)
        {
            if (currentUserId == otherUserId)
            {
                return null;
            }

            var areFriends = await _context.Friendships
                .AsNoTracking()
                .AnyAsync(
                    friendship =>
                        friendship.Status == FriendshipStatus.Accepted &&
                        ((friendship.RequesterId == currentUserId && friendship.AddresseeId == otherUserId) ||
                         (friendship.RequesterId == otherUserId && friendship.AddresseeId == currentUserId)),
                    cancellationToken);

            if (!areFriends)
            {
                _logger.LogWarning(
                    "User {UserId} tried to start a conversation with non-friend {OtherUserId}.",
                    currentUserId,
                    otherUserId);
                return null;
            }

            var user1Id = Math.Min(currentUserId, otherUserId);
            var user2Id = Math.Max(currentUserId, otherUserId);

            var existing = await _context.Conversations
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    conversation => conversation.User1Id == user1Id && conversation.User2Id == user2Id,
                    cancellationToken);

            if (existing != null)
            {
                return await GetDetailsAsync(existing.Id, currentUserId, cancellationToken);
            }

            var conversation = new Conversation
            {
                User1Id = user1Id,
                User2Id = user2Id,
                CreatedAt = DateTimeOffset.UtcNow,
                LastMessageAt = DateTimeOffset.UtcNow
            };

            _context.Conversations.Add(conversation);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                // Unique index protects from two simultaneous "Start" requests.
                _logger.LogDebug(ex, "Conversation already created for {User1Id}/{User2Id}.", user1Id, user2Id);
                var raced = await _context.Conversations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        item => item.User1Id == user1Id && item.User2Id == user2Id,
                        cancellationToken);

                return raced == null
                    ? null
                    : await GetDetailsAsync(raced.Id, currentUserId, cancellationToken);
            }

            return await GetDetailsAsync(conversation.Id, currentUserId, cancellationToken);
        }

        public async Task<ConversationDetailsDto?> GetDetailsAsync(
            int conversationId,
            int currentUserId,
            CancellationToken cancellationToken = default)
        {
            var conversation = await _context.Conversations
                .AsNoTracking()
                .Include(item => item.User1)
                .Include(item => item.User2)
                .FirstOrDefaultAsync(
                    item =>
                        item.Id == conversationId &&
                        (item.User1Id == currentUserId || item.User2Id == currentUserId),
                    cancellationToken);

            if (conversation == null)
            {
                return null;
            }

            var other = conversation.User1Id == currentUserId
                ? conversation.User2
                : conversation.User1;

            return new ConversationDetailsDto
            {
                Id = conversation.Id,
                OtherUserId = other.Id,
                OtherUsername = other.Username,
                OtherBio = other.Bio
            };
        }

        public async Task<IReadOnlyList<ConversationDto>> GetConversationsAsync(
            int userId,
            CancellationToken cancellationToken = default)
        {
            return await _context.Conversations
                .AsNoTracking()
                .Where(conversation =>
                    conversation.User1Id == userId || conversation.User2Id == userId)
                .OrderByDescending(conversation => conversation.LastMessageAt)
                .Select(conversation => new ConversationDto
                {
                    Id = conversation.Id,
                    OtherUserId = conversation.User1Id == userId
                        ? conversation.User2Id
                        : conversation.User1Id,
                    OtherUsername = conversation.User1Id == userId
                        ? conversation.User2.Username
                        : conversation.User1.Username,
                    OtherBio = conversation.User1Id == userId
                        ? conversation.User2.Bio
                        : conversation.User1.Bio,
                    LastMessageText = conversation.Messages
                        .OrderByDescending(message => message.Id)
                        .Select(message => message.Text)
                        .FirstOrDefault(),
                    LastMessageAt = conversation.LastMessageAt,
                    UnreadCount = conversation.Messages.Count(message =>
                        message.SenderId != userId && message.ReadAt == null)
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<DirectMessageDto>> GetMessagesAsync(
            int conversationId,
            int userId,
            int? afterId = null,
            CancellationToken cancellationToken = default)
        {
            var isParticipant = await _context.Conversations
                .AsNoTracking()
                .AnyAsync(
                    conversation =>
                        conversation.Id == conversationId &&
                        (conversation.User1Id == userId || conversation.User2Id == userId),
                    cancellationToken);

            if (!isParticipant)
            {
                return Array.Empty<DirectMessageDto>();
            }

            var query = _context.DirectMessages
                .AsNoTracking()
                .Where(message => message.ConversationId == conversationId);

            if (afterId.HasValue)
            {
                return await query
                    .Where(message => message.Id > afterId.Value)
                    .OrderBy(message => message.Id)
                    .Take(MessagePageSize)
                    .Select(message => new DirectMessageDto
                    {
                        Id = message.Id,
                        ConversationId = message.ConversationId,
                        SenderId = message.SenderId,
                        SenderName = message.Sender.Username,
                        Text = message.Text,
                        SentAt = message.SentAt
                    })
                    .ToListAsync(cancellationToken);
            }

            var messages = await query
                .OrderByDescending(message => message.Id)
                .Take(MessagePageSize)
                .Select(message => new DirectMessageDto
                {
                    Id = message.Id,
                    ConversationId = message.ConversationId,
                    SenderId = message.SenderId,
                    SenderName = message.Sender.Username,
                    Text = message.Text,
                    SentAt = message.SentAt
                })
                .ToListAsync(cancellationToken);

            messages.Reverse();
            return messages;
        }

        public async Task<DirectMessageDto?> SendMessageAsync(
            int conversationId,
            int userId,
            string senderName,
            string text,
            CancellationToken cancellationToken = default)
        {
            text = text?.Trim() ?? string.Empty;
            if (text.Length is < 1 or > MaxMessageLength)
            {
                return null;
            }

            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(
                    item =>
                        item.Id == conversationId &&
                        (item.User1Id == userId || item.User2Id == userId),
                    cancellationToken);

            if (conversation == null)
            {
                return null;
            }

            var message = new DirectMessage
            {
                ConversationId = conversationId,
                SenderId = userId,
                Text = text,
                SentAt = DateTimeOffset.UtcNow
            };

            conversation.LastMessageAt = message.SentAt;
            _context.DirectMessages.Add(message);
            await _context.SaveChangesAsync(cancellationToken);
            _metrics.DirectMessages.Inc();

            return new DirectMessageDto
            {
                Id = message.Id,
                ConversationId = message.ConversationId,
                SenderId = message.SenderId,
                SenderName = senderName,
                Text = message.Text,
                SentAt = message.SentAt
            };
        }

        public async Task<bool> MarkReadAsync(
            int conversationId,
            int userId,
            CancellationToken cancellationToken = default)
        {
            var isParticipant = await _context.Conversations
                .AsNoTracking()
                .AnyAsync(
                    conversation =>
                        conversation.Id == conversationId &&
                        (conversation.User1Id == userId || conversation.User2Id == userId),
                    cancellationToken);

            if (!isParticipant)
            {
                return false;
            }

            var now = DateTimeOffset.UtcNow;
            await _context.DirectMessages
                .Where(message =>
                    message.ConversationId == conversationId &&
                    message.SenderId != userId &&
                    message.ReadAt == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(message => message.ReadAt, now),
                    cancellationToken);

            return true;
        }
    }
}
