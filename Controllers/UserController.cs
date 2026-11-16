using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TestingAppWeb.Bots.ChatBots;
using TestingAppWeb.Extensions;
using TestingAppWeb.Interfaces;
using TestingAppWeb.Models;
using TestingAppWeb.Models.ViewModels;

namespace TestingAppWeb.Controllers
{
    public class UserController : Controller
    {
        private readonly IUserService _userService;
        private readonly IFriendshipService _friendshipService;

        public UserController(IUserService userService, IFriendshipService friendshipService)
        {
            _userService = userService;
            _friendshipService = friendshipService;
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminPanel()
        {
            var botsManager = await _userService.GetBotsManager();

            ViewBag.BotsManager = botsManager;

            return View();
        }

        private IActionResult Redirectback()
        {
            var referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(referer) && Url.IsLocalUrl(referer))
                return Redirect(referer);

            return RedirectToAction("Index", "Home");
        }

        [Authorize]
        public async Task<IActionResult> Profile(string userId)
        {
            if (!int.TryParse(userId, out var profileId))
            {
                return NotFound();
            }

            var user = await _userService.GetUserById(userId);
            if (user == null)
            {
                return NotFound();
            }

            var currentUserId = User.GetUserId();
            if (currentUserId == null)
            {
                return Challenge();
            }

            var isOwnProfile = currentUserId.Value == user.Id;
            var isAdmin = User.IsInRole("Admin");
            var friendship = isOwnProfile
                ? null
                : await _friendshipService.GetFriendshipAsync(currentUserId.Value, user.Id);

            var viewModel = new UserProfileViewModel
            {
                Username = user.Username,
                Email = isOwnProfile || isAdmin ? user.Email : null,
                Role = isOwnProfile || isAdmin ? user.Role : null,
                IsOwnProfile = isOwnProfile,
                UserId = user.Id.ToString(),
                Bio = user.Bio,
                FriendsCount = await _friendshipService.GetFriendCountAsync(user.Id),
                FriendshipId = friendship?.FriendshipId,
                FriendshipStatus = friendship?.Status,
                IsIncomingRequest = friendship?.IsIncomingRequest ?? false
            };

            return View(viewModel);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> EditProfile(string id, string returnUrl = null)
        {
            if (!int.TryParse(id, out int userId))
                return BadRequest();

            var user = await _userService.GetUserById(id);
            if (user == null)
                return NotFound();

            if (user.Username != User.Identity?.Name)
            {
                return Redirectback();
            }

            var viewModel = new EditProfileViewModel
            {
                Username = user.Username,
                Bio = user.Bio
            };

            ViewData["ReturnUrl"] = returnUrl;
            return View(viewModel);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(string id, EditProfileViewModel model, string returnUrl = null)
        {
            if (!int.TryParse(id, out int userId))
                return BadRequest();

            var user = await _userService.GetUserById(id);
            if (user == null)
                return NotFound();

            if (user.Username != User.Identity?.Name)
            {
                TempData["Error"] = "Доступ запрещён.";
                return RedirectToReturnUrl(returnUrl, () => RedirectToAction("Profile", new { userId = id }));
            }

            if (ModelState.IsValid)
            {
                await _userService.EditProfile(id, model.Username, model.Bio);

                var updatedUser = await _userService.GetUserById(id);
                if (updatedUser == null)
                    return NotFound();

                if (!await UpdateClaims(updatedUser))
                    return BadRequest();
                    

                TempData["Success"] = "Профиль успешно обновлён!";
                return RedirectToReturnUrl(returnUrl, () => RedirectToAction("Profile", new { userId = id }));
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View(model);
        }

        private async Task<bool> UpdateClaims(User updatedUser)
        {
            var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, updatedUser.Id.ToString()),
                    new Claim(ClaimTypes.Name, updatedUser.Username),
                    new Claim(ClaimTypes.Email, updatedUser.Email),
                    new Claim(ClaimTypes.Role, updatedUser.Role)
                };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(2)
            });

            return true;
        }

        private IActionResult RedirectToReturnUrl(string returnUrl, Func<IActionResult> fallback)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            return fallback();
        }

        [HttpGet]
        public IActionResult Login(string returnUrl = "/Home/Index")
        {
            var model = new LoginViewModel
            {
                ReturnUrl = returnUrl
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = "/Home/Index")
        {
            if (ModelState.IsValid)
            {
                User user = null;
                if (model.UserName.Contains("@"))
                {
                    user = await _userService.GetUserByMailAsync(model.UserName);
                }
                else
                {
                    user = await _userService.GetUserByUsernameAsync(model.UserName);
                }

                if (user != null && _userService.VerifyPassword(model.Password, user.PasswordHash))
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                        new Claim(ClaimTypes.Name, user.Username),
                        new Claim(ClaimTypes.Email, user.Email),
                        new Claim(ClaimTypes.Role, user.Role)
                    };

                    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var principal = new ClaimsPrincipal(identity);

                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        principal,
                        new AuthenticationProperties
                        {
                            IsPersistent = model.RememberMe
                        });

                    if (Url.IsLocalUrl(returnUrl))
                        return Redirect(returnUrl);
                    else
                        return RedirectToAction("Index", "Home");
                }

                ModelState.AddModelError(string.Empty, "Wrong password or username");
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var success = await _userService.RegisterUserAsync(model);

                if (success)
                {
                    return RedirectToAction("Login");
                }

                ModelState.AddModelError("Username", "Such user or email already exists");
            }

            return View(model);
        }

        public async Task<IActionResult> Logout()
        {
            await _userService.LogoutAsync(User.Identity?.Name ?? "unknown");
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        public IActionResult AccessDenied()
        {
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TurnBotOn(string botName)
        {
            var botsManager = HttpContext.RequestServices.GetRequiredService<ChatBotsManager>();
            botsManager.TurnBotOn(botName);
            return RedirectToAction("AdminPanel");
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TurnBotOff(string botName)
        {
            var botsManager = HttpContext.RequestServices.GetRequiredService<ChatBotsManager>();
            botsManager.TurnBotOff(botName);
            return RedirectToAction("AdminPanel");
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<bool> IsBotOn(string botName)
        {
            var botsManager = HttpContext.RequestServices.GetRequiredService<ChatBotsManager>();

            return botsManager.IsBotOn(botName);
        }
    }
}