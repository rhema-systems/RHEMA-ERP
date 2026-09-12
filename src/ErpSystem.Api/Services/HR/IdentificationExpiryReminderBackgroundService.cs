using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.HR;

/// <summary>
/// Daily host for the identification-expiry sweep (lane 3b).
/// </summary>
/// <remarks>
/// <para>⚠ <b>Without this host the feature does not exist.</b> Lane 1 found two HR sweeps that had
/// never run because nothing hosted them — the engine was there, the button was there, and the
/// nightly pass was not. A reminder engine that runs only when somebody remembers to press a button
/// is a report, not a reminder.</para>
///
/// <para>All sweep logic lives in <see cref="IIdentificationExpiryReminderService"/>, so the
/// HR-gated run-now endpoint exercises exactly the code path this host does — the same
/// host/processor split every other HR engine uses.</para>
///
/// <para>Nothing here mutates a card. The sweep raises reminders; renewing a document stays a
/// deliberate act on the employee's record.</para>
/// </remarks>
public sealed class IdentificationExpiryReminderBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IdentificationExpiryReminderBackgroundService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(24);

    public IdentificationExpiryReminderBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<IdentificationExpiryReminderBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Identification Expiry Reminder Background Service is starting");

        try
        {
            // Staggered clear of the movement, discipline, probation, travel, asset and separation
            // sweeps so a cold start does not run every HR engine at once. 23 minutes puts this
            // after separation's 17.
            await Task.Delay(TimeSpan.FromMinutes(23), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during identification expiry reminder processing");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Identification Expiry Reminder Background Service is stopping due to cancellation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in Identification Expiry Reminder Background Service");
            throw;
        }
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var lockService = scope.ServiceProvider.GetRequiredService<IDistributedLockService>();

        // One instance sweeps. Without the lease, every replica would raise the same reminders and
        // race on the dedupe key.
        await using var leader = await lockService.TryAcquireAsync(
            lockName: "bg:identification-expiry-reminders",
            leaseDuration: TimeSpan.FromMinutes(30),
            cancellationToken: cancellationToken);

        if (leader == null)
        {
            _logger.LogDebug("Skipping identification expiry run (lock not acquired)");
            return;
        }

        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var reminderService = scope.ServiceProvider.GetRequiredService<IIdentificationExpiryReminderService>();

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
                _logger.LogError(ex, "Identification expiry sweep failed for tenant {TenantId}", tenantId);
            }
        }
    }
}
