namespace smart_pet_care_api.Modules.ChatModule.Recovery;

public sealed class ChatPendingMessageRecoveryService(
    IServiceScopeFactory scopeFactory,
    ILogger<ChatPendingMessageRecoveryService> logger) : BackgroundService
{
    private static readonly TimeSpan RecoveryInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var recovery = scope.ServiceProvider
                    .GetRequiredService<ChatPendingMessageRecovery>();
                var recovered = await recovery.RecoverStalePendingMessagesAsync(
                    stoppingToken);

                if (recovered > 0)
                {
                    logger.LogWarning(
                        "Recovered {MessageCount} stale pending chat messages",
                        recovered);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Failed to recover stale pending chat messages");
            }

            await Task.Delay(RecoveryInterval, stoppingToken);
        }
    }
}
