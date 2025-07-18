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
            var result = new List<ChatMessageDto>();

            try
            {
                var allMessages = _context.ChatMessages
                    .Include(m => m.Sender)
                    .ToList();

                foreach (var msg in allMessages)
                {
                    if (msg.SenderId == null)
                    {
                        _logger.LogWarning("Error handling message");
                        continue;
                    }

                    string text = msg.MessageText;
                    DateTime timestamp = msg.SentAt;
                    string userName = msg.Sender.Username;

                    bool isValid = !string.IsNullOrEmpty(text) &&
                                   timestamp != default &&
                                   !string.IsNullOrEmpty(userName);

                    if (!isValid)
                    {
                        _logger.LogError($"Wrong data catched: {text}, {timestamp}, {userName}");
                        continue;
                    }

                    result.Add(new ChatMessageDto
                    {
                        Text = text,
                        Timestamp = timestamp,
                        UserName = userName
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing messages");
                return result;
            }
        }
    }
}
