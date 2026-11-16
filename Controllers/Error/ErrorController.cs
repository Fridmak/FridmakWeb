using Microsoft.AspNetCore.Mvc;

namespace TestingAppWeb.Controllers.Error
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
