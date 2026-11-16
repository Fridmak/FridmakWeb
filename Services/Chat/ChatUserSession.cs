using System.Collections.Concurrent;
using TestingAppWeb.Models.Chat;

namespace TestingAppWeb.Services.Chat
{
    public class ChatUserSession
    {
        private readonly ConcurrentQueue<(ChatMessage Message, MessageAction Action)> _outgoing = new();
        private readonly ConcurrentQueue<(ChatMessage Message, MessageAction Action)> _incoming = new();

        public ChatUserSession(int userId, ILogger<ChatUserSession> logger)
        {
            UserId = userId;
            Logger = logger;
        }

        public int UserId { get; }

        public ILogger<ChatUserSession> Logger { get; }

        public void EnqueueOutgoing((ChatMessage Message, MessageAction Action) message)
        {
            _outgoing.Enqueue(message);
        }

        public bool TryDequeueOutgoing(out (ChatMessage Message, MessageAction Action) task)
        {
            return _outgoing.TryDequeue(out task);
        }

        public void EnqueueIncoming((ChatMessage Message, MessageAction Action) update)
        {
            _incoming.Enqueue(update);
        }

        public IEnumerable<(ChatMessage Message, MessageAction Action)> DequeueIncomingUpdates()
        {
            while (_incoming.TryDequeue(out var update))
            {
                yield return update;
            }
        }
    }
}
