using System.Collections.Concurrent;

namespace TestingAppWeb.Services.Chat
{
    public class ChatSessionManager
    {
        private readonly ConcurrentDictionary<int, ChatUserSession> _sessions = new();
        private readonly ILogger<ChatSessionManager> _logger;
        private readonly ILoggerFactory _loggerFactory;

        public ChatSessionManager(
            ILogger<ChatSessionManager> logger,
            ILoggerFactory loggerFactory)
        {
            _logger = logger;
            _loggerFactory = loggerFactory;
        }

        public ChatUserSession GetOrCreateSession(int userId)
        {
            if (_sessions.TryGetValue(userId, out var existing))
            {
                return existing;
            }

            var session = new ChatUserSession(
                userId,
                _loggerFactory.CreateLogger<ChatUserSession>());

            if (_sessions.TryAdd(userId, session))
            {
                _logger.LogDebug("Created chat session for user {UserId}.", userId);
                return session;
            }

            return _sessions[userId];
        }

        public bool TryGetSession(int userId, out ChatUserSession session)
        {
            return _sessions.TryGetValue(userId, out session!);
        }

        public void RemoveSession(int userId)
        {
            _sessions.TryRemove(userId, out _);
        }

        public IEnumerable<int> GetAllUserIds() => _sessions.Keys;
    }
}
