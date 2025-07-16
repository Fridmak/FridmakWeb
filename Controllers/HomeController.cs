using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using TestingAppWeb.Data;
using TestingAppWeb.Models;

namespace TestingAppWeb.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AppDbContext _context;

        public HomeController(ILogger<HomeController> logger, AppDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult FriendList()
        {
            var friends = _context.Friends.ToList();
            return View(friends);
        }

        public IActionResult AddFriend()
        {
            return View();
        }

        [HttpPost]
        public IActionResult AddFriendForm(Friend friend)
        {
            if (ModelState.IsValid)
            {
                _context.Friends.Add(friend);
                _context.SaveChanges();
                return RedirectToAction("Index");
            }

            return View("Add", friend);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
