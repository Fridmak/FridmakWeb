using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TestingAppWeb.Extensions;
using TestingAppWeb.Models.Messaging;
using TestingAppWeb.Services.Messaging;

namespace TestingAppWeb.Controllers.Messages
{
    [Authorize]
    [AutoValidateAntiforgeryToken]
    public class MessagesController : Controller
    {
        private const int MaxMessageLength = 2000;

        private readonly IConversationService _conversationService;

        public MessagesController(IConversationService conversationService)
        {
            _conversationService = conversationService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var conversations = await _conversationService.GetConversationsAsync(
                User.GetRequiredUserId(),
                HttpContext.RequestAborted);

            return View(conversations);
        }

        [HttpGet]
        public async Task<IActionResult> Conversation(int id)
        {
            var userId = User.GetRequiredUserId();
            var conversation = await _conversationService.GetDetailsAsync(
                id,
                userId,
                HttpContext.RequestAborted);

            if (conversation == null)
            {
                return NotFound();
            }

            var messages = await _conversationService.GetMessagesAsync(
                id,
                userId,
                cancellationToken: HttpContext.RequestAborted);

            await _conversationService.MarkReadAsync(
                id,
                userId,
                HttpContext.RequestAborted);

            return View(new ConversationPageViewModel
            {
                Conversation = conversation,
                Messages = messages,
                CurrentUserId = userId
            });
        }

        [HttpGet]
        public async Task<IActionResult> Start(int userId)
        {
            var conversation = await _conversationService.GetOrCreateAsync(
                User.GetRequiredUserId(),
                userId,
                HttpContext.RequestAborted);

            if (conversation == null)
            {
                TempData["FriendMessage"] = "Переписка доступна только друзьям.";
                return RedirectToAction("Index", "Friends");
            }

            return RedirectToAction(nameof(Conversation), new { id = conversation.Id });
        }

        [HttpGet]
        public async Task<IActionResult> GetMessages(int conversationId, int? afterId)
        {
            var messages = await _conversationService.GetMessagesAsync(
                conversationId,
                User.GetRequiredUserId(),
                afterId,
                HttpContext.RequestAborted);

            return Json(messages);
        }

        [HttpPost]
        public async Task<IActionResult> Send([FromBody] SendDirectMessageRequest? request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Text))
            {
                return BadRequest(new { success = false, error = "Сообщение пустое." });
            }

            if (request.Text.Length > MaxMessageLength)
            {
                return BadRequest(new
                {
                    success = false,
                    error = $"Сообщение должно быть не длиннее {MaxMessageLength} символов."
                });
            }

            var message = await _conversationService.SendMessageAsync(
                request.ConversationId,
                User.GetRequiredUserId(),
                User.Identity?.Name ?? "unknown",
                request.Text,
                HttpContext.RequestAborted);

            if (message == null)
            {
                return BadRequest(new { success = false, error = "Не удалось отправить сообщение." });
            }

            return Ok(message);
        }

        [HttpPost]
        public async Task<IActionResult> MarkRead([FromBody] MarkConversationReadRequest? request)
        {
            if (request == null)
            {
                return BadRequest();
            }

            await _conversationService.MarkReadAsync(
                request.ConversationId,
                User.GetRequiredUserId(),
                HttpContext.RequestAborted);

            return Ok(new { success = true });
        }
    }
}
