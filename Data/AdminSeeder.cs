using Microsoft.EntityFrameworkCore;
using TestingAppWeb.Models;
using TestingAppWeb.Options;

namespace TestingAppWeb.Data
{
    public static class AdminSeeder
    {
        public static async Task SeedAsync(
            IServiceProvider services,
            SeedOptions options,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(options.AdminUsername) ||
                string.IsNullOrWhiteSpace(options.AdminPassword))
            {
                return;
            }

            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var logger = scope.ServiceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("AdminSeeder");

            var existingUser = await db.Users
                .FirstOrDefaultAsync(
                    user => user.Username == options.AdminUsername,
                    cancellationToken);

            if (existingUser != null)
            {
                if (existingUser.Role != "Admin")
                {
                    logger.LogWarning(
                        "User {Username} already exists but has role {Role}. Admin seed skipped.",
                        options.AdminUsername,
                        existingUser.Role);
                }

                return;
            }

            if (await db.Users.AnyAsync(user => user.Role == "Admin", cancellationToken))
            {
                logger.LogInformation("An admin user already exists. Admin seed skipped.");
                return;
            }

            db.Users.Add(new User
            {
                Username = options.AdminUsername,
                Email = options.AdminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(options.AdminPassword),
                Role = "Admin",
                Bio = string.Empty
            });

            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Admin user {Username} created.", options.AdminUsername);
        }
    }
}
