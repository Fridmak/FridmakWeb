using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TestingAppWeb.Models;
using TestingAppWeb.Services;

namespace TestingAppWeb.Controllers
{
    public class AccountController : Controller
    {
        private readonly IUserService _userService;

        public AccountController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        public IActionResult Login(string returnUrl = "/Home/Index")
        {
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            LoginViewModel model,
            string returnUrl = "/Home/Index")
        {
            if (!ModelState.IsValid)
            {
                model.ReturnUrl = returnUrl;
                return View(model);
            }

            var user = model.UserName?.Contains('@') == true
                ? await _userService.GetUserByMailAsync(
                    model.UserName,
                    HttpContext.RequestAborted)
                : await _userService.GetUserByUsernameAsync(
                    model.UserName ?? string.Empty,
                    HttpContext.RequestAborted);

            if (user == null || !_userService.VerifyPassword(model.Password, user.PasswordHash))
            {
                ModelState.AddModelError(string.Empty, "Wrong password or username");
                model.ReturnUrl = returnUrl;
                return View(model);
            }

            var principal = CreatePrincipal(user);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties { IsPersistent = model.RememberMe });

            return Url.IsLocalUrl(returnUrl)
                ? Redirect(returnUrl)
                : RedirectToAction("Index", "Home");
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
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var success = await _userService.RegisterUserAsync(
                model,
                HttpContext.RequestAborted);

            if (success)
            {
                return RedirectToAction(nameof(Login));
            }

            ModelState.AddModelError("Username", "Such user or email already exists");
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

        private static ClaimsPrincipal CreatePrincipal(User user)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.Username),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Role, user.Role)
            };

            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            return new ClaimsPrincipal(identity);
        }
    }
}
