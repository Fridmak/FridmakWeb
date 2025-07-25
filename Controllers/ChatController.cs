using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TestingAppWeb.Data;
using TestingAppWeb.Interfaces;
using TestingAppWeb.Models;
using TestingAppWeb.Services;

namespace TestingAppWeb.Controllers
{
    public class ChatController : Controller
    {
        private readonly IChatService _chatService;

        public ChatController(IChatService chatService)
        {
            _chatService = chatService;
        }

        public async Task<IActionResult> Index()
        {
            var messages = await _chatService.GetRecentMessagesAsync();
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
        public async Task<IActionResult> GetMessages()
        {
            var messages = await _chatService.GetRecentMessagesAsync();
            return Ok(messages);
        }
    }
}