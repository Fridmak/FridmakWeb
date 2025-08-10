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

        public UserService(AppDbContext context, ILogger<UserService> logger, IServiceProvider serviceProvider)
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
                .FirstOrDefaultAsync(u => u.Username == model.Username);

            if (existingUser != null)
            {
                return false;
            }

            var user = new User
            {
                Username = model.Username,
                Email = model.Email,
                PasswordHash = GetPasswordHash(model.Password),
                Role = "User"
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

        public async Task LogoutAsync(string userName)
        {
            _logger.Log(LogLevel.Information, $"user {userName} logged out");
            return;
        }

        public async Task<bool> AcceptFriendAsync(int id)
        {
            var friend = await _context.Friends.FindAsync(id);
            if (friend == null) return false;

            friend.Approved = true;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RejectFriendAsync(int id)
        {
            var friend = await _context.Friends.FindAsync(id);
            if (friend == null) return false;

            _context.Friends.Remove(friend);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<Friend>> GetFriendsToApproveAsync()
        {
            var res = await _context.Friends.Where(x => !x.Approved).ToListAsync();

            return res.Count != 0? res : null;
        }

        public async Task<ChatBotsManager> GetBotsManager()
        {
            return _serviceProvider.GetRequiredService<ChatBotsManager>();
        }
    }
}