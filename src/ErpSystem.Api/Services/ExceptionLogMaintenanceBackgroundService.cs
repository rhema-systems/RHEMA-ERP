using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services;

/// <summary>
/// Periodically purges old exception logs from SQL to control table growth.
/// Uses LastOccurredAt so deduplicated fingerprints stay as long as they keep happening.
/// </summary>
public sealed class ExceptionLogMaintenanceBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ExceptionLogMaintenanceBackgroundService> _logger;

    public ExceptionLogMaintenanceBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<ExceptionLogMaintenanceBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run shortly after startup, then daily.
        await SafeRunOnceAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await SafeRunOnceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown - do not propagate.
        }
    }

    private async Task SafeRunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            var resolvedRetentionDays = GetInt("ExceptionLogs:RetentionDaysResolved", 90);
            var unresolvedRetentionDays = GetInt("ExceptionLogs:RetentionDaysUnresolved", 180);
            var deletedRetentionDays = GetInt("ExceptionLogs:RetentionDaysDeleted", 7);

            var now = DateTime.UtcNow;
            var resolvedCutoff = now.AddDays(-resolvedRetentionDays);
            var unresolvedCutoff = now.AddDays(-unresolvedRetentionDays);
            var deletedCutoff = now.AddDays(-deletedRetentionDays);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Note: IgnoreQueryFilters to include IsDeleted rows (global soft delete filter).
            var resolvedQuery = db.SystemExceptionLogs
                .IgnoreQueryFilters()
                .Where(x => !x.IsDeleted && x.IsResolved && x.LastOccurredAt < resolvedCutoff);

            var unresolvedQuery = db.SystemExceptionLogs
                .IgnoreQueryFilters()
                .Where(x => !x.IsDeleted && !x.IsResolved && x.LastOccurredAt < unresolvedCutoff);

            var deletedQuery = db.SystemExceptionLogs
                .IgnoreQueryFilters()
                .Where(x => x.IsDeleted && (x.DeletedAt ?? x.LastOccurredAt) < deletedCutoff);

            var resolvedDeleted = await resolvedQuery.ExecuteDeleteAsync(stoppingToken);
            var unresolvedDeleted = await unresolvedQuery.ExecuteDeleteAsync(stoppingToken);
            var deletedPurged = await deletedQuery.ExecuteDeleteAsync(stoppingToken);

            if (resolvedDeleted + unresolvedDeleted + deletedPurged > 0)
            {
                _logger.LogInformation(
                    "Exception log retention purge complete. Resolved={ResolvedDeleted}, Unresolved={UnresolvedDeleted}, DeletedPurged={DeletedPurged}",
                    resolvedDeleted,
                    unresolvedDeleted,
                    deletedPurged);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception log retention purge failed");
        }
    }

    private int GetInt(string key, int fallback)
    {
        var value = _configuration[key];
        return int.TryParse(value, out var i) && i > 0 ? i : fallback;
    }
}

