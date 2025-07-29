using System.Collections.Concurrent;
using TestingAppWeb.Models;
using TestingAppWeb.Models.Chat;

public class ChatHandler
{
    private readonly ConcurrentStack<(ChatMSG Message, MessageAction Action)> _messagesToUpdateToServer = new();
    private readonly ConcurrentStack<(ChatMSG Message, MessageAction Action)> _messagesToUpdateToClient = new();
    private readonly int _id;
    private readonly ILogger<ChatHandler> _logger;

    public ChatHandler(int id, ILogger<ChatHandler> logger)
    {
        _id = id;
        _logger = logger;
    }

    public void AddMessage((ChatMSG Message, MessageAction Action) message)
    {
        _messagesToUpdateToServer.Push(message);
    }

    public void AddMessage(Tuple<ChatMSG, MessageAction> tuple)
        => AddMessage((tuple.Item1, tuple.Item2));

    public bool TryDequeueOutgoingTask(out (ChatMSG Message, MessageAction Action) task)
    {
        return _messagesToUpdateToServer.TryPop(out task);
    }

    public void EnqueueIncomingUpdate((ChatMSG Message, MessageAction Action) update)
    {
        _messagesToUpdateToClient.Push(update);
    }

    public IEnumerable<(ChatMSG Message, MessageAction Action)> GetMessagesToUpdate()
    {
        while (!_messagesToUpdateToClient.IsEmpty)
        {
            if (_messagesToUpdateToClient.TryPop(out var update))
                yield return update;
        }
        yield break;
    }
}