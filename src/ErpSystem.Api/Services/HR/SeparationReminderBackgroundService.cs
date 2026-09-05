using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.HR;

/// <summary>
/// Daily host for the separation reminder engine (area 9b slice 10, FR-HR-111).
///
/// All sweep logic lives in <see cref="ISeparationReminderService"/> so the HR-gated run-now
/// endpoint (POST api/hr/separations/reminders/run) exercises exactly the code path this host does —
/// the same host/processor split every other HR engine uses.
/// </summary>
/// <remarks>
/// <para>⚠ <b>This is the sweep FR-HR-093 depends on.</b> Retirement at 60 with advance alerts, and
/// a contract running out with nobody warned, only reach anybody through this pass. Until it was
/// hosted, the whole engine ran when — and only when — somebody remembered to press the button.</para>
///
/// <para>Nothing here mutates an employee: the sweep raises reminders. Raising the separations
/// themselves stays a deliberate act on the retirement and contract-expiry sweep endpoints.</para>
/// </remarks>
public sealed class SeparationReminderBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SeparationReminderBackgroundService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(24);

    public SeparationReminderBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<SeparationReminderBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Separation Reminder Background Service is starting");

        try
        {
            // Staggered clear of the movement, discipline, probation, travel and asset sweeps so a
            // cold start does not run every HR engine at once.
            await Task.Delay(TimeSpan.FromMinutes(17), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during separation reminder processing");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Separation Reminder Background Service is stopping due to cancellation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in Separation Reminder Background Service");
            throw;
        }
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var lockService = scope.ServiceProvider.GetRequiredService<IDistributedLockService>();

        await using var leader = await lockService.TryAcquireAsync(
            lockName: "bg:separation-reminders",
            leaseDuration: TimeSpan.FromMinutes(30),
            cancellationToken: cancellationToken);

        if (leader == null)
        {
            _logger.LogDebug("Skipping separation reminder run (lock not acquired)");
            return;
        }

        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var reminderService = scope.ServiceProvider.GetRequiredService<ISeparationReminderService>();

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
                _logger.LogError(ex, "Separation reminder sweep failed for tenant {TenantId}", tenantId);
            }
        }
    }
}
