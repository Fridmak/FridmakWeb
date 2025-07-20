using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Threading.Tasks;
using TestingAppWeb.Data;
using TestingAppWeb.Interfaces;
using TestingAppWeb.Models;

namespace TestingAppWeb.Controllers
{
    public class HomeController : Controller
    {
        private readonly IHomeService _homeService;

        public HomeController(IHomeService homeService)
        {
            _homeService = homeService;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        public async Task<IActionResult> FriendList()
        {
            var friends = await _homeService.GetFriendListAsync();

            return View(friends);
        }

        public IActionResult AddFriend()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AddFriendForm(Friend friend)
        {
            if (!ModelState.IsValid)
            {
                return View(friend);
            }

            var success = await _homeService.AddFriendRequestAsync(friend);

            if (success)
            {
                return RedirectToAction("FriendList");
            }
            else
            {
                ModelState.AddModelError("", "Unable to add friend.");
                return View(friend);
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
