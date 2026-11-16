namespace TestingAppWeb.Services.Chat
{
    public class ChatBackgroundService : BackgroundService
    {
        private static readonly TimeSpan ProcessingInterval = TimeSpan.FromMilliseconds(500);

        private readonly ChatMessageProcessor _processor;
        private readonly ILogger<ChatBackgroundService> _logger;

        public ChatBackgroundService(
            ChatMessageProcessor processor,
            ILogger<ChatBackgroundService> logger)
        {
            _processor = processor;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _processor.SetupBotsAsync(stoppingToken);
                    await _processor.ProcessPendingMessagesAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Chat background processing failed.");
                }

                try
                {
                    await Task.Delay(ProcessingInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
