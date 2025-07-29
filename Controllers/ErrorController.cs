using Microsoft.AspNetCore.Mvc;

namespace TestingAppWeb.Controllers
{
    public class ErrorController : Controller
    {
        [NonAction]
        public IActionResult Forbidden()
        {
            return View();
        }
        [NonAction]
        public IActionResult TooManyRequests()
        {
            return View();
        }
    }
}
