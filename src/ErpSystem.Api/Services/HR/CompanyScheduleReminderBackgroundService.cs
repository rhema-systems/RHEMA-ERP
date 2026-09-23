using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.HR;

/// <summary>
/// Hourly host for the company-schedule reminder sweep (round 4, lane N-b2).
///
/// All sweep logic lives in <see cref="ICompanyEventService.SendDueRemindersAsync"/>, so the run-now
/// endpoint (POST api/CompanySchedule/reminders/run) exercises exactly the code path this host does —
/// the host/processor split every HR reminder engine uses.
///
/// Before this, the event form's <b>Send reminders</b> and <b>days before</b> were saved and read by
/// nothing, and the RSVP chase went only when HR pressed its button.
///
/// Hourly, where lane K's sweep is daily: an event created this morning for tomorrow, reminding a day
/// before, is due today — a daily pass could fall before it existed and come round again after the
/// event. Each reminder is stamped on its event when sent, so cadence affects latency only, never
/// duplicates.
///
/// ⚠ Registered with <c>AddHostedService</c> in the same change as this class: two HR sweeps in this
/// codebase turned out never to have run because that line was missing.
/// </summary>
public sealed class CompanyScheduleReminderBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CompanyScheduleReminderBackgroundService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(1);

    public CompanyScheduleReminderBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<CompanyScheduleReminderBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Company Schedule Reminder Background Service is starting");

        try
        {
            // Staggered: the other HR sweeps start at 3–29 minutes; 23 was free.
            await Task.Delay(TimeSpan.FromMinutes(23), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during company schedule reminder processing");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Company Schedule Reminder Background Service is stopping due to cancellation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in Company Schedule Reminder Background Service");
            throw;
        }
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var lockService = scope.ServiceProvider.GetRequiredService<IDistributedLockService>();

        await using var leader = await lockService.TryAcquireAsync(
            lockName: "bg:company-schedule-reminders",
            leaseDuration: TimeSpan.FromMinutes(20),
            cancellationToken: cancellationToken);

        if (leader == null)
        {
            _logger.LogDebug("Skipping company schedule reminder run (lock not acquired)");
            return;
        }

        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var events = scope.ServiceProvider.GetRequiredService<ICompanyEventService>();

        var tenants = await unitOfWork.Repository<Tenant>()
            .GetQueryable(t => !t.IsDeleted)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        foreach (var tenantId in tenants)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await events.SendDueRemindersAsync(tenantId, DateTime.UtcNow, cancellationToken);
            }
            catch (Exception ex)
            {
                // One tenant's failure must not starve the rest.
                _logger.LogError(ex, "Company schedule reminder sweep failed for tenant {TenantId}", tenantId);
            }
        }
    }
}
