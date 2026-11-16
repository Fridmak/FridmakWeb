using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TestingAppWeb.Interfaces;
using TestingAppWeb.Models.Chat;

namespace TestingAppWeb.Controllers
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
            var messagesWithActions = await GetMessagesAndActions(true);
            var messages = messagesWithActions.Select(msg => msg.Item1).ToList();
            return View(messages);
        }

        [HttpPost]
        public async Task<IActionResult> EditMessage([FromBody] EditMessageRequest? request)
        {
            if (request is null)
            {
                return BadRequest(new { success = false, error = "Invalid request" });
            }

            var result = await _chatService.EditMessageAsync(request);

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

            var success = await _chatService.SendMessageAsync(messageDto);

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
            var messages = await GetMessagesAndActions(loadOld);
            var formattedMessages = messages.Select(m => new object[] { m.Item1, m.Item2 }).ToList();

            return Ok(formattedMessages);
        }

        private async Task<List<(ChatMessageDto, MessageAction)>> GetMessagesAndActions(bool loadOld)
        {
            return await _chatService.GetMessagesToUpdateAsync(loadOld);
        }
    }
}
