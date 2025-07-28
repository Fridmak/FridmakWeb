using System;
using TestingAppWeb.Data;
using TestingAppWeb.Models;

namespace TestingAppWeb.Services
{
    public class ChatHandler
    {
        private readonly int _userId;
        private readonly AppDbContext _context;
        private Queue<(ChatMSG Message, MessageAction Action)> _messagesToUpdateToServer;
        private Queue<(ChatMSG Message, MessageAction Action)> _messagesToUpdateFromServer;

        public ChatHandler(int userId, AppDbContext context)
        {
            _userId = userId;
            _context = context;
        }

        public async Task SolveTasks()
        {
            foreach (var task in _messagesToUpdateFromServer)
            {
                switch (task.Action)
                {
                    case MessageAction.Send:

                        break;
                    case MessageAction.Delete:

                        break;
                    case MessageAction.Edit:

                        break;
                }
            }

            await _context.SaveChangesAsync();
        }

        public async Task AddMessage(IEnumerable<(ChatMSG Message, MessageAction Action)> messagesToUpdate)
        {
            foreach (var message in messagesToUpdate)
            {
                await AddMessage(message);
            }
        }

        public async Task AddMessage((ChatMSG Message, MessageAction Action) message)
        {
            _messagesToUpdateToServer.Enqueue(message);
        }
    }
}
