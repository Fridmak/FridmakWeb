using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TestingAppWeb.Extensions;
using TestingAppWeb.Services.Messaging;
using TestingAppWeb.Services.Observability;

namespace TestingAppWeb.Hubs
{
    [Authorize]
    public class MessagesHub : Hub
    {
        private readonly IConversationService _conversationService;
        private readonly ILogger<MessagesHub> _logger;
        private readonly AppMetrics _metrics;

        public MessagesHub(
            IConversationService conversationService,
            ILogger<MessagesHub> logger,
            AppMetrics metrics)
        {
            _conversationService = conversationService;
            _logger = logger;
            _metrics = metrics;
        }

        public async Task JoinConversation(int conversationId)
        {
            var userId = Context.User!.GetRequiredUserId();
            var conversation = await _conversationService.GetDetailsAsync(
                conversationId,
                userId,
                Context.ConnectionAborted);

            if (conversation == null)
            {
                throw new HubException("Ð”Ð¸Ð°Ð»Ð¾Ð³ Ð½Ðµ Ð½Ð°Ð¹Ð´ÐµÐ½.");
            }

            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                ConversationGroup(conversationId));

            await _conversationService.MarkReadAsync(
                conversationId,
                userId,
                Context.ConnectionAborted);

            await Clients.User(conversation.OtherUserId.ToString())
                .SendAsync("ConversationRead", new { conversationId, userId });
        }

        public async Task LeaveConversation(int conversationId)
        {
            await Groups.RemoveFromGroupAsync(
                Context.ConnectionId,
                ConversationGroup(conversationId));
        }

        public async Task SendMessage(int conversationId, string text)
        {
            var userId = Context.User!.GetRequiredUserId();
            var senderName = Context.User?.Identity?.Name ?? "unknown";

            var message = await _conversationService.SendMessageAsync(
                conversationId,
                userId,
                senderName,
                text,
                Context.ConnectionAborted);

            if (message == null)
            {
                throw new HubException("ÐÐµ ÑƒÐ´Ð°Ð»Ð¾ÑÑŒ Ð¾Ñ‚Ð¿Ñ€Ð°Ð²Ð¸Ñ‚ÑŒ ÑÐ¾Ð¾Ð±Ñ‰ÐµÐ½Ð¸Ðµ.");
            }

            await Clients.Group(ConversationGroup(conversationId))
                .SendAsync("MessageReceived", message);

            var conversation = await _conversationService.GetDetailsAsync(
                conversationId,
                userId,
                Context.ConnectionAborted);

            if (conversation != null)
            {
                await Clients.User(conversation.OtherUserId.ToString())
                    .SendAsync("ConversationUpdated", message);
            }
        }

        public async Task MarkRead(int conversationId)
        {
            var userId = Context.User!.GetRequiredUserId();
            var success = await _conversationService.MarkReadAsync(
                conversationId,
                userId,
                Context.ConnectionAborted);

            if (!success)
            {
                return;
            }

            var conversation = await _conversationService.GetDetailsAsync(
                conversationId,
                userId,
                Context.ConnectionAborted);

            if (conversation != null)
            {
                await Clients.User(conversation.OtherUserId.ToString())
                    .SendAsync("ConversationRead", new { conversationId, userId });
            }
        }

        public override async Task OnConnectedAsync()
        {
            _metrics.SignalRConnections.Inc();

            _logger.LogDebug(
                "User {UserId} connected to messages hub. ConnectionId={ConnectionId}",
                Context.UserIdentifier,
                Context.ConnectionId);

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _metrics.SignalRConnections.Dec();

            _logger.LogDebug(
                "User {UserId} disconnected from messages hub. ConnectionId={ConnectionId}",
                Context.UserIdentifier,
                Context.ConnectionId);

            await base.OnDisconnectedAsync(exception);
        }

        private static string ConversationGroup(int conversationId)
        {
            return $"conversation:{conversationId}";
        }
    }
}
