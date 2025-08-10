using System.Collections.Concurrent;
using TestingAppWeb.Bots.ChatBots;
using TestingAppWeb.Interfaces;
using TestingAppWeb.Models.Chat;

public class ChatHandler
{
    private readonly ConcurrentStack<(ChatMSG Message, MessageAction Action)> _messagesToUpdateToServer = new();
    private readonly ConcurrentStack<(ChatMSG Message, MessageAction Action)> _messagesToUpdateToClient = new();
    private readonly int _id;
    private readonly ILogger<ChatHandler> _logger;
    private readonly IServiceProvider _serviceProvider;

    public ChatHandler(int id, IServiceProvider serviceProvider)
    {
        _id = id;
        _serviceProvider = serviceProvider;

        _logger = CreateLogger();
    }

    private ILogger<ChatHandler> CreateLogger()
    {
        return _serviceProvider.GetRequiredService<ILogger<ChatHandler>>();
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