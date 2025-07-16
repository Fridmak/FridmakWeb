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
            var messages = _context.ChatMessages
                .Include(m => m.Sender)
                .ToList();
            return View(messages);
        }

        public IActionResult AdminPanel()
        {
            return View();
        }

        [HttpPost]
        public IActionResult SendMessage([FromBody] ChatMSG message)
        {
            message.SentAt = DateTime.Now;
            message.Sender = new User();
            message.Sender.Username = "Anonim poka"; // ToDo: тут временно
            _context.ChatMessages.Add(message);
            _context.SaveChanges();
            return Json(new { success = true });
        }

        [HttpGet]
        public IActionResult GetMessages()
        {
            var messages = _context.ChatMessages
                .Select(m => new {
                    m.MessageText,
                    timestamp = m.SentAt,
                    userName = m.Sender.Username
                })
                .ToList();

            return Json(messages);
        }
    }
}
