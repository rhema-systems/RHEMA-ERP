namespace ErpSystem.Api.Services.Finance.GL;

/// <summary>
/// Materializes due occurrences and posts only exact, checker-authorised automatic
/// reversals on every API node. Original occurrences always retain human approval
/// and posting controls; database claiming and idempotency protect concurrent nodes.
/// </summary>
public sealed class RecurringJournalBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RecurringJournalBackgroundService> _logger;

    public RecurringJournalBackgroundService(IServiceScopeFactory scopeFactory, ILogger<RecurringJournalBackgroundService> logger)
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
            var processor = scope.ServiceProvider.GetRequiredService<RecurringJournalGenerationProcessor>();
            var result = await processor.ProcessAllAsync(DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken);
            if (result.GeneratedCount > 0 || result.FailedCount > 0)
                _logger.LogInformation("Recurring-journal scheduler generated {GeneratedCount}, found {ExistingCount} existing, and failed {FailedCount}.",
                    result.GeneratedCount, result.ExistingCount, result.FailedCount);
            var reversals = scope.ServiceProvider.GetRequiredService<RecurringJournalReversalProcessor>();
            var reversalResult = await reversals.ProcessAllAsync(DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken);
            if (reversalResult.PostedCount > 0 || reversalResult.FailedCount > 0)
                _logger.LogInformation(
                    "Recurring-journal scheduler posted {PostedCount} authorised reversals, recovered {ExistingCount}, and failed {FailedCount}.",
                    reversalResult.PostedCount, reversalResult.ExistingCount, reversalResult.FailedCount);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected during normal host shutdown.
        }
        catch (Exception exception)
        {
            // Scheduling must remain retryable and must never prevent the API host
            // from starting. The next interval safely rechecks the same due dates.
            _logger.LogError(exception, "Recurring-journal background processing failed.");
        }
    }
}
