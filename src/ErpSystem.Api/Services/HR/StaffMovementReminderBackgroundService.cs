using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.HR;

/// <summary>
/// Daily host for the staff-movement reminder engine (area 8 slice 5).
///
/// All sweep logic lives in <see cref="IStaffMovementReminderService"/> so the HR-gated run-now
/// endpoint (POST api/staff-movements/reminders/run) exercises exactly the code path this host does
/// — the same host/processor split the SHE and Finance engines use.
///
/// Daily rather than hourly, unlike SHE: nothing here is shift-grained. The finest thing this sweep
/// watches is a date — an assignment ending, an effective date arriving — and a reminder about a
/// date does not become more useful for being repeated every hour. Per-item dedupe means the
/// cadence only affects latency, never duplicates.
/// </summary>
public sealed class StaffMovementReminderBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StaffMovementReminderBackgroundService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(24);

    public StaffMovementReminderBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<StaffMovementReminderBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Staff Movement Reminder Background Service is starting");

        try
        {
            // Staggered against the other HR hosts so a cold start does not run every sweep at once.
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during staff movement reminder processing");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Staff Movement Reminder Background Service is stopping due to cancellation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in Staff Movement Reminder Background Service");
            throw;
        }
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var lockService = scope.ServiceProvider.GetRequiredService<IDistributedLockService>();

        await using var leader = await lockService.TryAcquireAsync(
            lockName: "bg:staff-movement-reminders",
            leaseDuration: TimeSpan.FromMinutes(30),
            cancellationToken: cancellationToken);

        if (leader == null)
        {
            _logger.LogDebug("Skipping staff movement reminder run (lock not acquired)");
            return;
        }

        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var reminderService = scope.ServiceProvider.GetRequiredService<IStaffMovementReminderService>();

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
                _logger.LogError(ex, "Staff movement reminder sweep failed for tenant {TenantId}", tenantId);
            }
        }
    }
}
