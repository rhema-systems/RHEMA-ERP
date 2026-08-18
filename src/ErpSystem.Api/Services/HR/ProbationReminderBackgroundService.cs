using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.HR;

/// <summary>
/// Daily host for the probation reminder engine (area 15b slice 7).
///
/// All sweep logic lives in <see cref="IProbationReminderService"/> so the HR-gated run-now
/// endpoint (POST api/probations/reminders/run) exercises exactly the code path this host does —
/// the same host/processor split the SHE, movement and Finance engines use.
///
/// Daily rather than hourly: the finest thing swept is a date - a probation end, a review due date
/// - and a reminder about a date does not become more useful for being repeated every hour.
/// Per-item dedupe means cadence affects latency only, never duplicates.
/// </summary>
public sealed class ProbationReminderBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ProbationReminderBackgroundService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(24);

    public ProbationReminderBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ProbationReminderBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Probation Reminder Background Service is starting");

        try
        {
            // Staggered behind the movement sweep so a cold start does not run every HR sweep at once.
            await Task.Delay(TimeSpan.FromMinutes(7), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during probation reminder processing");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Probation Reminder Background Service is stopping due to cancellation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in Probation Reminder Background Service");
            throw;
        }
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var lockService = scope.ServiceProvider.GetRequiredService<IDistributedLockService>();

        await using var leader = await lockService.TryAcquireAsync(
            lockName: "bg:probation-reminders",
            leaseDuration: TimeSpan.FromMinutes(30),
            cancellationToken: cancellationToken);

        if (leader == null)
        {
            _logger.LogDebug("Skipping probation reminder run (lock not acquired)");
            return;
        }

        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var reminderService = scope.ServiceProvider.GetRequiredService<IProbationReminderService>();

        var tenants = await unitOfWork.Repository<Tenant>()
            .GetQueryable(t => !t.IsDeleted)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        foreach (var tenantId in tenants)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await reminderService.RunSweepForTenantAsync(
                    tenantId, "Scheduled", triggeredByUserId: null, cancellationToken);
            }
            catch (Exception ex)
            {
                // One tenant's failure must not starve the rest.
                _logger.LogError(ex, "Probation reminder sweep failed for tenant {TenantId}", tenantId);
            }
        }
    }
}
