using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.HR;

/// <summary>
/// Daily host for the leave reminder engine (closure plan wave E, slice E2).
///
/// <para>All sweep logic lives in <see cref="ILeaveReminderService"/> so the run-now endpoint
/// (POST api/hr/leave/reminders/run) exercises exactly the code path this host does — the same
/// host/processor split the SHE, movement, discipline and Finance engines use.</para>
///
/// <para>Daily rather than hourly: the finest thing swept is a date, and a reminder about a date
/// does not become more useful for being repeated every hour. Per-item dedupe means cadence affects
/// latency only, never duplicates.</para>
/// </summary>
public sealed class LeaveReminderBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LeaveReminderBackgroundService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(24);

    public LeaveReminderBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<LeaveReminderBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Leave Reminder Background Service is starting");

        try
        {
            // Staggered behind the other HR sweeps so a cold start does not run them all at once.
            // Leave is the twelfth engine, so it goes last in the stagger.
            await Task.Delay(TimeSpan.FromMinutes(17), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during leave reminder processing");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Leave Reminder Background Service is stopping due to cancellation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in Leave Reminder Background Service");
            throw;
        }
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var lockService = scope.ServiceProvider.GetRequiredService<IDistributedLockService>();

        await using var leader = await lockService.TryAcquireAsync(
            lockName: "bg:leave-reminders",
            leaseDuration: TimeSpan.FromMinutes(30),
            cancellationToken: cancellationToken);

        if (leader == null)
        {
            _logger.LogDebug("Skipping leave reminder run (lock not acquired)");
            return;
        }

        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var reminderService = scope.ServiceProvider.GetRequiredService<ILeaveReminderService>();
        var leaveService = scope.ServiceProvider.GetRequiredService<ILeaveService>();

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
                _logger.LogError(ex, "Leave reminder sweep failed for tenant {TenantId}", tenantId);
            }

            // Converge the attendance register on the leave statuses that actually hold.
            //
            // ⚠ This is here rather than as another hook on purpose. A leave status can be changed
            // through doors this module does not own — the generic workflow recall calls the status
            // adapter directly and never touches LeaveService — and a hook per door is a list
            // somebody eventually forgets to extend. A convergent sweep needs no such list.
            // Anything it repairs is logged as a WARNING, because drift means a real bug upstream.
            try
            {
                await leaveService.ReconcileRecentAttendanceAsync(
                    tenantId, lookbackDays: 14, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Leave attendance reconciliation failed for tenant {TenantId}", tenantId);
            }
        }
    }
}
