using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TestingAppWeb.Extensions;
using TestingAppWeb.Models.Chat;
using TestingAppWeb.Services.Chat;

namespace TestingAppWeb.Controllers.Chat
{
    [Authorize]
    [AutoValidateAntiforgeryToken]
    public class ChatController : Controller
    {
        private const int MaxMessageLength = 2000;

        private readonly IChatService _chatService;

        public ChatController(IChatService chatService)
        {
            _chatService = chatService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var messages = await _chatService.GetMessagesToUpdateAsync(
                User.GetRequiredUserId(),
                loadOld: true,
                cancellationToken: HttpContext.RequestAborted);

            return View(messages.Select(message => message.Item1).ToList());
        }

        [HttpPost]
        public async Task<IActionResult> EditMessage([FromBody] EditMessageRequest? request)
        {
            if (request is null)
            {
                return BadRequest(new { success = false, error = "Invalid request" });
            }

            var result = await _chatService.EditMessageAsync(
                User.GetRequiredUserId(),
                User.IsInRole("Admin"),
                request,
                HttpContext.RequestAborted);

            return result switch
            {
                MessageEditResult.Success => Ok(new { success = true }),
                MessageEditResult.NotFound => NotFound(new { success = false, error = "Message not found" }),
                MessageEditResult.Forbidden => StatusCode(
                    StatusCodes.Status403Forbidden,
                    new { success = false, error = "You can only edit your own messages" }),
                MessageEditResult.Invalid => BadRequest(
                    new { success = false, error = $"Message must be between 1 and {MaxMessageLength} characters" }),
                _ => StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new { success = false, error = "Internal server error" })
            };
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage([FromBody] ChatMessageDto? messageDto)
        {
            if (messageDto is null || string.IsNullOrWhiteSpace(messageDto.Text))
            {
                return BadRequest(new { success = false, error = "Message is empty" });
            }

            if (messageDto.Text.Length > MaxMessageLength)
            {
                return BadRequest(
                    new { success = false, error = $"Message is too long (max {MaxMessageLength} characters)" });
            }

            var success = await _chatService.SendMessageAsync(
                User.GetRequiredUserId(),
                messageDto,
                HttpContext.RequestAborted);

            if (success)
            {
                return Ok(new { success = true });
            }

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { success = false, error = "Failed to send message" });
        }

        [HttpGet]
        public async Task<IActionResult> GetMessages(bool loadOld = false)
        {
            var messages = await _chatService.GetMessagesToUpdateAsync(
                User.GetRequiredUserId(),
                loadOld,
                HttpContext.RequestAborted);

            var formattedMessages = messages
                .Select(message => new object[] { message.Item1, message.Item2 })
                .ToList();

            return Ok(formattedMessages);
        }
    }
}
