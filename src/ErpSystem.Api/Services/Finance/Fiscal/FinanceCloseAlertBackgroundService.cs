namespace ErpSystem.Api.Services.Finance.Fiscal;

/// <summary>
/// Periodically invokes the Finance close alert processor. All duplicate prevention and retry
/// state lives in FinanceCloseAlertDelivery, so this host is deliberately stateless and safe to
/// run on every API node.
/// </summary>
public sealed class FinanceCloseAlertBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromMinutes(15);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FinanceCloseAlertBackgroundService> _logger;

    public FinanceCloseAlertBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<FinanceCloseAlertBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await SafeRunOnceAsync(stoppingToken);
        using var timer = new PeriodicTimer(PollingInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await SafeRunOnceAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected during normal application shutdown.
        }
    }

    private async Task SafeRunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var processor = scope.ServiceProvider.GetRequiredService<FinanceCloseAlertService>();
            var result = await processor.ProcessDueAlertsAsync(cancellationToken: cancellationToken);
            if (result.DeliveredCount > 0 || result.FailedCount > 0)
            {
                _logger.LogInformation(
                    "Finance close alert run considered {CandidateCount} candidates, delivered {DeliveredCount}, skipped {SkippedCount}, and failed {FailedCount}.",
                    result.CandidateCount,
                    result.DeliveredCount,
                    result.SkippedCount,
                    result.FailedCount);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected during normal application shutdown.
        }
        catch (Exception exception)
        {
            // Alert delivery is operationally important but cannot be allowed to terminate the
            // API host or change an accounting-period state. The next interval retries safely.
            _logger.LogError(exception, "Finance period-close alert processing failed.");
        }
    }
}
