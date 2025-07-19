using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TestingAppWeb.Data;
using TestingAppWeb.Models;

namespace TestingAppWeb.Controllers
{
    public class ChatController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AppDbContext _context;
        private readonly int _messagesToShow = 500;

        public ChatController(ILogger<HomeController> logger, AppDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public IActionResult Index()
        {
            var messages = FilterMessages();
            return View(messages);
        }

        [HttpPost]
        public IActionResult EditMessage([FromBody] EditMessageRequest request)
        {
            if (request == null)
            {
                return BadRequest(new { success = false, error = "Invalid request" });
            }

            var msg = _context.ChatMessages
                .FirstOrDefault(m => m.Id == request.Id);

            if (msg == null)
            {
                return Ok(new { success = false, error = "Message not found" });
            }

            if (request.Delete)
            {
                _context.Remove(msg);
            }

            var newText = request.Delete ? "DELETED" : request.Message;
            var commet = request.Delete ? $"DELETED: Time={DateTime.Now} | Comment={request.Message}" : request.Message;
            _logger.LogInformation($"Message edited: ID={msg.Id}, OldText={msg.MessageText}, NewText={newText}, Comment={request.Comment}");
            msg.MessageText = request.Message;

            try
            {
                _context.SaveChanges();

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while editing message with ID={Id}", request.Id);
                return StatusCode(500, new { success = false, error = "Internal server error" });
            }
        }

        [HttpPost]
        public IActionResult SendMessage([FromBody] ChatMessageDto message)
        {
            if (!User.Identity.IsAuthenticated )
                return RedirectToAction("Login", "User");

            var msg = new ChatMSG();
            msg.SentAt = DateTime.UtcNow;
            msg.Sender = _context.Users.FirstOrDefault(user => user.Username == User.Identity.Name);
            msg.MessageText = message.Text;
            _context.ChatMessages.Add(msg);
            _context.SaveChanges();

            return Json(new { success = true });
        }

        [HttpGet]
        public IActionResult GetMessages()
        {
            var messages = FilterMessages();

            return Json(messages);
        }

        private List<ChatMessageDto> FilterMessages()
        {
            try
            {
                var result = _context.ChatMessages
                    .Include(m => m.Sender)
                    .Where(m => m.SenderId != null &&
                                !string.IsNullOrEmpty(m.MessageText) &&
                                m.SentAt != default(DateTime) &&
                                !string.IsNullOrEmpty(m.Sender.Username))
                    .OrderByDescending(m => m.SentAt)
                    .Take(_messagesToShow)
                    .Select(m => new ChatMessageDto
                    {
                        Text = m.MessageText,
                        Timestamp = m.SentAt,
                        UserName = m.Sender.Username,
                        MessageId = m.Id
                    })
                    .Reverse()
                    .ToList();

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing messages");
                return new List<ChatMessageDto>();
            }
        }
    }
}
