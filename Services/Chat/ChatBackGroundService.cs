public class ChatBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;

    public ChatBackgroundService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _serviceProvider.CreateScope();
            var chatServer = scope.ServiceProvider.GetRequiredService<ChatServer>();

            try
            {
                await chatServer.SetupBots();
                await chatServer.ProcessAllPendingMessages();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<ChatBackgroundService>>();
                logger.LogError(ex, "Error in ChatBackgroundService");
            }

            try
            {
                await Task.Delay(500, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
