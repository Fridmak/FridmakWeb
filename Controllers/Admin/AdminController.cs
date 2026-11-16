using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TestingAppWeb.Options;
using TestingAppWeb.Services.Chat.Bots;
using TestingAppWeb.Services.Observability;

namespace TestingAppWeb.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ChatBotManager _botManager;
        private readonly IPrometheusMetricsService _metricsService;
        private readonly PrometheusOptions _prometheusOptions;

        public AdminController(
            ChatBotManager botManager,
            IPrometheusMetricsService metricsService,
            IOptions<PrometheusOptions> prometheusOptions)
        {
            _botManager = botManager;
            _metricsService = metricsService;
            _prometheusOptions = prometheusOptions.Value;
        }

        [HttpGet]
        public IActionResult Index()
        {
            ViewBag.BotsManager = _botManager;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Metrics()
        {
            var snapshot = await _metricsService.GetSnapshotAsync(HttpContext.RequestAborted);
            ViewBag.GrafanaUrl = _prometheusOptions.GrafanaUrl;
            return View(snapshot);
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
