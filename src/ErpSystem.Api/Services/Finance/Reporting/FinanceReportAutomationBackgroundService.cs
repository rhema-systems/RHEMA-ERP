namespace ErpSystem.Api.Services.Finance.Reporting;

/// <summary>
/// Executes due Finance report schedules without requiring an open browser.
/// Idempotency is enforced by the execution occurrence key and row versions,
/// allowing all API nodes to run this lightweight poller safely.
/// </summary>
public sealed class FinanceReportAutomationBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FinanceReportAutomationBackgroundService> _logger;

    public FinanceReportAutomationBackgroundService(
        IServiceScopeFactory scopeFactory, ILogger<FinanceReportAutomationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunSafelyAsync(stoppingToken);
        using var timer = new PeriodicTimer(Interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken)) await RunSafelyAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected during normal host shutdown.
        }
    }

    private async Task RunSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var processor = scope.ServiceProvider.GetRequiredService<FinanceReportAutomationProcessor>();
            var result = await processor.ProcessDueAsync(DateTime.UtcNow, null, cancellationToken);
            if (result.SucceededCount > 0 || result.FailedCount > 0)
                _logger.LogInformation("Finance report automation completed {Succeeded} and failed {Failed} due schedules.",
                    result.SucceededCount, result.FailedCount);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected during normal host shutdown.
        }
        catch (Exception exception)
        {
            // One scheduler fault must not prevent the ERP API from starting;
            // due occurrences remain durable and retry on the next interval.
            _logger.LogError(exception, "Finance report automation background processing failed.");
        }
    }
}
