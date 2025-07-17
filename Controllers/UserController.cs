using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TestingAppWeb.Data;
using TestingAppWeb.Models;

namespace TestingAppWeb.Controllers
{
    public class UserController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AppDbContext _context;

        public UserController(ILogger<HomeController> logger, AppDbContext context)
        {
            _logger = logger;
            _context = context;
        }
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Chat()
        {
            var messages = FilterMessages();
            return View(messages);
        }

        public IActionResult AdminPanel()
        {
            return View();
        }

        [HttpPost]
        public IActionResult SendMessage([FromBody] ChatMessageDto message)
        {
            var msg = new ChatMSG();
            msg.SentAt = DateTime.UtcNow;
            msg.Sender = new User();
            msg.MessageText = message.Text;
            msg.Sender.Username = message.UserName;
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
                var allMessages = _context.ChatMessages.ToList();

                foreach (var msg in allMessages)
                {
                    if (msg.Sender == null)
                    {
                        _logger.LogWarning("Сообщение пропущено: Отправитель не найден.");
                        continue;
                    }

                    string text = msg.MessageText;
                    DateTime timestamp = msg.SentAt;
                    string username = msg.Sender.Username;

                    bool isValid = !string.IsNullOrEmpty(text) &&
                                   timestamp != default &&
                                   !string.IsNullOrEmpty(username);

                    if (!isValid)
                    {
                        _logger.LogError($"Некорректное сообщение: {text}, {timestamp}, {username}");
                        continue;
                    }

                    result.Add(new ChatMessageDto
                    {
                        Text = text,
                        Timestamp = timestamp,
                        UserName = username
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при фильтрации сообщений");
                return result;
            }
        }
    }
}
