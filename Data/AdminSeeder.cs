using Microsoft.EntityFrameworkCore;
using TestingAppWeb.Models;

namespace TestingAppWeb.Data
{
    public static class AdminSeeder
    {
        public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
        {
            var username = configuration["Seed:AdminUsername"];
            var password = configuration["Seed:AdminPassword"];
            var email = configuration["Seed:AdminEmail"] ?? "admin@example.com";

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                return;
            }

            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var logger = scope.ServiceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("AdminSeeder");

            var existingUser = await db.Users
                .FirstOrDefaultAsync(user => user.Username == username);

            if (existingUser != null)
            {
                if (existingUser.Role != "Admin")
                {
                    logger.LogWarning(
                        "User '{Username}' already exists but has role '{Role}'. Admin seed skipped.",
                        username,
                        existingUser.Role);
                }

                return;
            }

            if (await db.Users.AnyAsync(user => user.Role == "Admin"))
            {
                logger.LogInformation("An admin user already exists. Admin seed skipped.");
                return;
            }

            db.Users.Add(new User
            {
                Username = username,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = "Admin",
                Bio = ""
            });

            await db.SaveChangesAsync();
            logger.LogInformation("Admin user '{Username}' created.", username);
        }
    }
}
