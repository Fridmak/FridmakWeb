using Microsoft.EntityFrameworkCore;

namespace TestingAppWeb.Extensions
{
    public static class DatabaseExtensions
    {
        public static async Task MigrateDatabaseAsync<TContext>(
            this IServiceProvider services,
            ILogger logger,
            int maxAttempts = 10,
            CancellationToken cancellationToken = default)
            where TContext : DbContext
        {
            using var scope = services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    await dbContext.Database.MigrateAsync(cancellationToken);
                    logger.LogInformation("Database migrations applied.");
                    return;
                }
                catch (Exception ex) when (attempt < maxAttempts)
                {
                    logger.LogWarning(
                        ex,
                        "Database migration attempt {Attempt}/{MaxAttempts} failed. Retrying in 3 seconds...",
                        attempt,
                        maxAttempts);
                    await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
                }
            }
        }
    }
}
