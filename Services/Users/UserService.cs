using Microsoft.EntityFrameworkCore;
using TestingAppWeb.Data;
using TestingAppWeb.Models;
using TestingAppWeb.Services.Observability;

namespace TestingAppWeb.Services.Users
{
    public interface IUserService
    {
        Task<User?> GetUserByUsernameAsync(
            string username,
            CancellationToken cancellationToken = default);

        Task<User?> GetUserByMailAsync(
            string email,
            CancellationToken cancellationToken = default);

        Task<User?> GetUserById(
            string id,
            CancellationToken cancellationToken = default);

        Task<bool> RegisterUserAsync(
            RegisterViewModel model,
            CancellationToken cancellationToken = default);

        Task<bool> RegisterUserAsync(
            string userName,
            string email,
            string password,
            CancellationToken cancellationToken = default);

        Task<bool> EditProfile(
            string id,
            string userName,
            string bio,
            CancellationToken cancellationToken = default);

        Task<bool> SetRoleAsync(
            int userId,
            string role,
            CancellationToken cancellationToken = default);

        string GetPasswordHash(string password);

        bool VerifyPassword(string password, string passwordHash);

        Task LogoutAsync(string userName);
    }

    public class UserService : IUserService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<UserService> _logger;
        private readonly AppMetrics _metrics;

        public UserService(
            AppDbContext context,
            ILogger<UserService> logger,
            AppMetrics metrics)
        {
            _context = context;
            _logger = logger;
            _metrics = metrics;
        }

        public async Task<User?> GetUserByUsernameAsync(
            string username,
            CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .FirstOrDefaultAsync(user => user.Username == username, cancellationToken);
        }

        public async Task<User?> GetUserByMailAsync(
            string email,
            CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .FirstOrDefaultAsync(user => user.Email == email, cancellationToken);
        }

        public async Task<User?> GetUserById(
            string id,
            CancellationToken cancellationToken = default)
        {
            if (!int.TryParse(id, out var userId))
            {
                return null;
            }

            return await _context.Users
                .FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);
        }

        public async Task<bool> RegisterUserAsync(
            RegisterViewModel model,
            CancellationToken cancellationToken = default)
        {
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(
                    user => user.Username == model.Username || user.Email == model.Email,
                    cancellationToken);

            if (existingUser != null)
            {
                return false;
            }

            _context.Users.Add(new User
            {
                Username = model.Username,
                Email = model.Email,
                PasswordHash = GetPasswordHash(model.Password),
                Role = "User",
                Bio = string.Empty
            });

            await _context.SaveChangesAsync(cancellationToken);
            _metrics.Registrations.Inc();
            return true;
        }

        public async Task<bool> RegisterUserAsync(
            string userName,
            string email,
            string password,
            CancellationToken cancellationToken = default)
        {
            return await RegisterUserAsync(
                new RegisterViewModel
                {
                    Username = userName,
                    Email = email,
                    Password = password
                },
                cancellationToken);
        }

        public async Task<bool> EditProfile(
            string id,
            string userName,
            string bio,
            CancellationToken cancellationToken = default)
        {
            var user = await GetUserById(id, cancellationToken);
            if (user == null)
            {
                return false;
            }

            user.Username = userName;
            user.Bio = bio;
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<bool> SetRoleAsync(
            int userId,
            string role,
            CancellationToken cancellationToken = default)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

            if (user == null)
            {
                return false;
            }

            user.Role = role;
            await _context.SaveChangesAsync(cancellationToken);
            return true;
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
            _logger.LogInformation("User {UserName} logged out.", userName);
            return Task.CompletedTask;
        }
    }
}
