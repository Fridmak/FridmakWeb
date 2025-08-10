using System.Collections.Concurrent;
using TestingAppWeb.Data;

namespace TestingAppWeb.Services.Chat
{
    public class ChatHandlerManager
    {
        private readonly ConcurrentDictionary<int, ChatHandler> _handlers = new();
        private readonly ILogger<ChatHandlerManager> _logger;
        private readonly IServiceProvider _serviceProvider;

        public ChatHandlerManager(IServiceProvider serviceProvider, ILogger<ChatHandlerManager> logger)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public ChatHandler GetOrCreateHandler(int userId)
        {
            return _handlers.GetOrAdd(userId, id => new ChatHandler(id, _serviceProvider));
        }

        public bool TryGetHandler(int userId, out ChatHandler handler)
        {
            return _handlers.TryGetValue(userId, out handler);
        }

        public void RemoveHandler(int userId)
        {
            _handlers.TryRemove(userId, out _);
        }

        public IEnumerable<int> GetAllUserIds() => _handlers.Keys;
    }
}
