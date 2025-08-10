using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TestingAppWeb.Data;
using TestingAppWeb.Interfaces;
using TestingAppWeb.Models;
using TestingAppWeb.Models.Chat;
using TestingAppWeb.Services;

namespace TestingAppWeb.Controllers
{
    public class ChatController : Controller
    {
        private readonly IChatService _chatService;
        private readonly string _botToken;

        public ChatController(IChatService chatService, IConfiguration configuration)
        {
            _chatService = chatService;
            _botToken = configuration["BotSettings:Token"]
               ?? throw new InvalidOperationException("Bot token is not configured.");
        }

        public async Task<IActionResult> Index()
        {
            var messagesWithActions = await GetMessagesAndActions(true);
            var messages = messagesWithActions.Select(msg => msg.Item1).ToList();
            return View(messages);
        }

        [HttpPost]
        public async Task<IActionResult> EditMessage([FromBody] EditMessageRequest request)
        {
            if (request == null)
            {
                return BadRequest(new { success = false, error = "Invalid request" });
            }

            var success = await _chatService.EditMessageAsync(request);

            if (success)
            {
                return Ok(new { success = true });
            }
            else
            {
                return StatusCode(500, new { success = false, error = "Internal server error" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage([FromBody] ChatMessageDto messageDto)
        {
            if (!User.Identity.IsAuthenticated)
                return Unauthorized();

            var username = User.Identity.Name;

            var success = await _chatService.SendMessageAsync(messageDto, username);

            if (success)
            {
                return Ok(new { success = true });
            }
            else
            {
                return StatusCode(500, new { success = false, error = "Failed to send message" });
            }
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