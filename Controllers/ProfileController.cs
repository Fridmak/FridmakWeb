using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TestingAppWeb.Extensions;
using TestingAppWeb.Models;
using TestingAppWeb.Models.ViewModels;
using TestingAppWeb.Services;

namespace TestingAppWeb.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly IUserService _userService;
        private readonly IFriendshipService _friendshipService;

        public ProfileController(
            IUserService userService,
            IFriendshipService friendshipService)
        {
            _userService = userService;
            _friendshipService = friendshipService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string userId)
        {
            if (!int.TryParse(userId, out _))
            {
                return NotFound();
            }

            var user = await _userService.GetUserById(
                userId,
                HttpContext.RequestAborted);

            if (user == null)
            {
                return NotFound();
            }

            var currentUserId = User.GetRequiredUserId();
            var isOwnProfile = currentUserId == user.Id;
            var isAdmin = User.IsInRole("Admin");
            var friendship = isOwnProfile
                ? null
                : await _friendshipService.GetFriendshipAsync(
                    currentUserId,
                    user.Id,
                    HttpContext.RequestAborted);

            var viewModel = new UserProfileViewModel
            {
                Username = user.Username,
                Email = isOwnProfile || isAdmin ? user.Email : null,
                Role = isOwnProfile || isAdmin ? user.Role : null,
                IsOwnProfile = isOwnProfile,
                UserId = user.Id.ToString(),
                Bio = user.Bio,
                FriendsCount = await _friendshipService.GetFriendCountAsync(
                    user.Id,
                    HttpContext.RequestAborted),
                FriendshipId = friendship?.FriendshipId,
                FriendshipStatus = friendship?.Status,
                IsIncomingRequest = friendship?.IsIncomingRequest ?? false
            };

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string id, string? returnUrl = null)
        {
            if (!int.TryParse(id, out var userId))
            {
                return BadRequest();
            }

            var user = await _userService.GetUserById(
                id,
                HttpContext.RequestAborted);

            if (user == null)
            {
                return NotFound();
            }

            if (user.Id != User.GetRequiredUserId())
            {
                return Forbid();
            }

            var viewModel = new EditProfileViewModel
            {
                Username = user.Username,
                Bio = user.Bio
            };

            ViewData["ReturnUrl"] = returnUrl;
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            string id,
            EditProfileViewModel model,
            string? returnUrl = null)
        {
            if (!int.TryParse(id, out var userId))
            {
                return BadRequest();
            }

            var user = await _userService.GetUserById(
                id,
                HttpContext.RequestAborted);

            if (user == null)
            {
                return NotFound();
            }

            if (user.Id != User.GetRequiredUserId())
            {
                TempData["Error"] = "Доступ запрещён.";
                return RedirectToReturnUrl(
                    returnUrl,
                    () => RedirectToAction(nameof(Index), new { userId = id }));
            }

            if (!ModelState.IsValid)
            {
                ViewData["ReturnUrl"] = returnUrl;
                return View(model);
            }

            await _userService.EditProfile(
                id,
                model.Username,
                model.Bio,
                HttpContext.RequestAborted);

            var updatedUser = await _userService.GetUserById(
                id,
                HttpContext.RequestAborted);

            if (updatedUser == null)
            {
                return NotFound();
            }

            await UpdateClaimsAsync(updatedUser);

            TempData["Success"] = "Профиль успешно обновлён!";
            return RedirectToReturnUrl(
                returnUrl,
                () => RedirectToAction(nameof(Index), new { userId = id }));
        }

        private async Task UpdateClaimsAsync(User updatedUser)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, updatedUser.Id.ToString()),
                new(ClaimTypes.Name, updatedUser.Username),
                new(ClaimTypes.Email, updatedUser.Email),
                new(ClaimTypes.Role, updatedUser.Role)
            };

            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(2)
                });
        }

        private IActionResult RedirectToReturnUrl(string? returnUrl, Func<IActionResult> fallback)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return fallback();
        }
    }
}
