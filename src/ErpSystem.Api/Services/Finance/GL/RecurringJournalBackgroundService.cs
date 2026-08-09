namespace ErpSystem.Api.Services.Finance.GL;

/// <summary>
/// Materializes due occurrences on every API node. Database uniqueness and
/// row-version checks make concurrent nodes safe; the worker never posts money and
/// therefore cannot bypass the human occurrence approval and posting controls.
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
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected during normal host shutdown.
        }
        catch (Exception exception)
        {
            // Scheduling must remain retryable and must never prevent the API host
            // from starting. The next interval safely rechecks the same due dates.
            _logger.LogError(exception, "Recurring-journal background generation failed.");
        }
    }
}
