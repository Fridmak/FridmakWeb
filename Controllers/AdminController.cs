using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TestingAppWeb.Services.Chat.Bots;

namespace TestingAppWeb.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ChatBotManager _botManager;

        public AdminController(ChatBotManager botManager)
        {
            _botManager = botManager;
        }

        [HttpGet]
        public IActionResult Index()
        {
            ViewBag.BotsManager = _botManager;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult TurnBotOn(string botName)
        {
            _botManager.TurnBotOn(botName);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult TurnBotOff(string botName)
        {
            _botManager.TurnBotOff(botName);
            return RedirectToAction(nameof(Index));
        }
    }
}
