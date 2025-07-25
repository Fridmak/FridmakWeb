using Microsoft.AspNetCore.Mvc;

namespace TestingAppWeb.Controllers
{
    public class ErrorController : Controller
    {
        public IActionResult Forbidden()
        {
            return View();
        }

        public IActionResult TooManyRequests()
        {
            return View();
        }
    }
}
