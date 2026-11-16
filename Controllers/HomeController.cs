using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using TestingAppWeb.Extensions;
using TestingAppWeb.Interfaces;
using TestingAppWeb.Models;
using TestingAppWeb.Models.ViewModels;

namespace TestingAppWeb.Controllers
{
    public class HomeController : Controller
    {
        private readonly IFriendshipService _friendshipService;

        public HomeController(IFriendshipService friendshipService)
        {
            _friendshipService = friendshipService;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        [Authorize]
        public async Task<IActionResult> FriendList()
        {
            var userId = User.GetUserId();
            if (userId == null)
            {
                return Challenge();
            }

            var model = new FriendsPageViewModel
            {
                Friends = await _friendshipService.GetFriendsAsync(userId.Value),
                IncomingRequests = await _friendshipService.GetIncomingRequestsAsync(userId.Value),
                OutgoingRequests = await _friendshipService.GetOutgoingRequestsAsync(userId.Value)
            };

            return View(model);
        }

        [Authorize]
        public async Task<IActionResult> AddFriend(string? query)
        {
            var userId = User.GetUserId();
            if (userId == null)
            {
                return Challenge();
            }

            var model = new AddFriendViewModel
            {
                Query = query,
                Results = string.IsNullOrWhiteSpace(query)
                    ? Array.Empty<UserSearchResult>()
                    : await _friendshipService.SearchUsersAsync(userId.Value, query)
            };

            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendFriendRequest(int userId, string? returnUrl = null)
        {
            var currentUserId = User.GetUserId();
            if (currentUserId == null)
            {
                return Challenge();
            }

            var result = await _friendshipService.SendRequestAsync(currentUserId.Value, userId);

            TempData["FriendMessage"] = result switch
            {
                FriendRequestResult.Success => "Заявка отправлена.",
                FriendRequestResult.AlreadyFriends => "Вы уже друзья.",
                FriendRequestResult.AlreadyPending => "Заявка уже отправлена или уже ждёт вашего ответа.",
                FriendRequestResult.CannotAddSelf => "Нельзя добавить самого себя.",
                FriendRequestResult.Blocked => "Пользователь недоступен для добавления.",
                _ => "Не удалось отправить заявку."
            };

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Profile", "User", new { userId });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptFriendRequest(int id)
        {
            var userId = User.GetUserId();
            if (userId == null)
            {
                return Challenge();
            }

            var success = await _friendshipService.AcceptRequestAsync(id, userId.Value);
            TempData["FriendMessage"] = success
                ? "Заявка принята."
                : "Не удалось принять заявку.";

            return RedirectToAction(nameof(FriendList));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectFriendRequest(int id)
        {
            var userId = User.GetUserId();
            if (userId == null)
            {
                return Challenge();
            }

            var success = await _friendshipService.RejectRequestAsync(id, userId.Value);
            TempData["FriendMessage"] = success
                ? "Заявка отклонена."
                : "Не удалось отклонить заявку.";

            return RedirectToAction(nameof(FriendList));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelFriendRequest(int id)
        {
            var userId = User.GetUserId();
            if (userId == null)
            {
                return Challenge();
            }

            var success = await _friendshipService.CancelRequestAsync(id, userId.Value);
            TempData["FriendMessage"] = success
                ? "Заявка отменена."
                : "Не удалось отменить заявку.";

            return RedirectToAction(nameof(FriendList));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveFriend(int id)
        {
            var userId = User.GetUserId();
            if (userId == null)
            {
                return Challenge();
            }

            var success = await _friendshipService.RemoveFriendAsync(id, userId.Value);
            TempData["FriendMessage"] = success
                ? "Пользователь удалён из друзей."
                : "Не удалось удалить пользователя из друзей.";

            return RedirectToAction(nameof(FriendList));
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
