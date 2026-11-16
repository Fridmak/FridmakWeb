using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TestingAppWeb.Extensions;
using TestingAppWeb.Models;
using TestingAppWeb.Models.ViewModels;
using TestingAppWeb.Services.Friends;

namespace TestingAppWeb.Controllers.Friends
{
    [Authorize]
    public class FriendsController : Controller
    {
        private readonly IFriendshipService _friendshipService;

        public FriendsController(IFriendshipService friendshipService)
        {
            _friendshipService = friendshipService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = User.GetRequiredUserId();
            var cancellationToken = HttpContext.RequestAborted;

            var model = new FriendsPageViewModel
            {
                Friends = await _friendshipService.GetFriendsAsync(userId, cancellationToken),
                IncomingRequests = await _friendshipService.GetIncomingRequestsAsync(userId, cancellationToken),
                OutgoingRequests = await _friendshipService.GetOutgoingRequestsAsync(userId, cancellationToken)
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Search(string? query)
        {
            var userId = User.GetRequiredUserId();

            var model = new AddFriendViewModel
            {
                Query = query,
                Results = string.IsNullOrWhiteSpace(query)
                    ? Array.Empty<UserSearchResult>()
                    : await _friendshipService.SearchUsersAsync(
                        userId,
                        query,
                        cancellationToken: HttpContext.RequestAborted)
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendRequest(int userId, string? returnUrl = null)
        {
            var currentUserId = User.GetRequiredUserId();

            var result = await _friendshipService.SendRequestAsync(
                currentUserId,
                userId,
                HttpContext.RequestAborted);

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

            return RedirectToAction("Index", "Profile", new { userId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptRequest(int id)
        {
            var success = await _friendshipService.AcceptRequestAsync(
                id,
                User.GetRequiredUserId(),
                HttpContext.RequestAborted);

            TempData["FriendMessage"] = success
                ? "Заявка принята."
                : "Не удалось принять заявку.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectRequest(int id)
        {
            var success = await _friendshipService.RejectRequestAsync(
                id,
                User.GetRequiredUserId(),
                HttpContext.RequestAborted);

            TempData["FriendMessage"] = success
                ? "Заявка отклонена."
                : "Не удалось отклонить заявку.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelRequest(int id)
        {
            var success = await _friendshipService.CancelRequestAsync(
                id,
                User.GetRequiredUserId(),
                HttpContext.RequestAborted);

            TempData["FriendMessage"] = success
                ? "Заявка отменена."
                : "Не удалось отменить заявку.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveFriend(int id)
        {
            var success = await _friendshipService.RemoveFriendAsync(
                id,
                User.GetRequiredUserId(),
                HttpContext.RequestAborted);

            TempData["FriendMessage"] = success
                ? "Пользователь удалён из друзей."
                : "Не удалось удалить пользователя из друзей.";

            return RedirectToAction(nameof(Index));
        }
    }
}
