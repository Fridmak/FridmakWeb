using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;
using TestingAppWeb.Bots.ChatBots;
using TestingAppWeb.Data;
using TestingAppWeb.Interfaces;
using TestingAppWeb.Models;

namespace TestingAppWeb.Services
{
    public class UserService : IUserService
    {
        private readonly AppDbContext _context;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<UserService> _logger;

        public UserService(
            AppDbContext context,
            ILogger<UserService> logger,
            IServiceProvider serviceProvider)
        {
            _context = context;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public async Task<User> GetUserByUsernameAsync(string username)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        }

        public async Task<bool> RegisterUserAsync(RegisterViewModel model)
        {
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == model.Username || u.Email == model.Email);

            if (existingUser != null)
            {
                return false;
            }

            var user = new User
            {
                Username = model.Username,
                Email = model.Email,
                PasswordHash = GetPasswordHash(model.Password),
                Role = "User",
                Bio = ""
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> RegisterUserAsync(string userName, string email, string password)
        {
            var model = new RegisterViewModel{
                Username = userName,
                Email = email,
                Password = password
            };
            return await RegisterUserAsync(model);
        }

        public string GetPasswordHash(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        public bool VerifyPassword(string password, string passwordHash)
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }

        public Task LogoutAsync(string userName)
        {
            _logger.Log(LogLevel.Information, $"user {userName} logged out");
            return Task.CompletedTask;
        }

        public async Task<bool> SetRoleAsync(int userId, string role)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return false;
            }

            user.Role = role;
            await _context.SaveChangesAsync();
            return true;
        }

        public Task<ChatBotsManager> GetBotsManager()
        {
            return Task.FromResult(_serviceProvider.GetRequiredService<ChatBotsManager>());
        }

        public async Task<bool> EditProfile(string id, string userName, string bio)
        {
            var user = await GetUserById(id);

            if (user == null)
            {
                return false;
            }

            user.Username = userName;
            user.Bio = bio;

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<User> GetUserById(string id)
        {
            if (!int.TryParse(id, out int userIdValue))
                return null;

            return await _context.Users.FirstOrDefaultAsync(user => user.Id == userIdValue);
        }

        public async Task<User> GetUserByMailAsync(string email)
        {
            return await _context.Users.FirstOrDefaultAsync(user => user.Email == email);
        }
    }
}