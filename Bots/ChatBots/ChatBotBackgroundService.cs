namespace TestingAppWeb.Bots.ChatBots
{
    public class ChatBotBackgroundService : BackgroundService
    {
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.CompletedTask;
        }
    }
}
