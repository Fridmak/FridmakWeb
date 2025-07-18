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
                        UserName = m.Sender.Username
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
