using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;
using TestingAppWeb.Bots.ChatBots;
using TestingAppWeb.Data;
using TestingAppWeb.Interfaces;
using TestingAppWeb.Models;

namespace TestingAppWeb.Controllers
{
    public class UserController : Controller
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminPanel()
        {
            var friendsToApprove = await _userService.GetFriendsToApproveAsync();
            var botsManager = await _userService.GetBotsManager();

            ViewBag.FriendsToApprove = friendsToApprove;
            ViewBag.BotsManager = botsManager;

            return View();
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> AcceptFriend(int id)
        {
            var success = await _userService.AcceptFriendAsync(id);
            return RedirectToAction("AdminPanel");
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> RejectFriend(int id)
        {
            var success = await _userService.RejectFriendAsync(id);
            return RedirectToAction("AdminPanel");
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
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = "/Home/Index")
        {
            if (ModelState.IsValid)
            {
                var user = await _userService.GetUserByUsernameAsync(model.UserName);

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

                ModelState.AddModelError("Username", "Such user already exists");
            }

            return View(model);
        }

        public async Task<IActionResult> Logout()
        {
            await _userService.LogoutAsync(User.Identity.Name);
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        public IActionResult AccessDenied()
        {
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> TurnBotOn(string botName)
        {
            var botsManager = HttpContext.RequestServices.GetRequiredService<ChatBotsManager>();
            botsManager.TurnBotOn(botName);
            return RedirectToAction("AdminPanel");
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
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