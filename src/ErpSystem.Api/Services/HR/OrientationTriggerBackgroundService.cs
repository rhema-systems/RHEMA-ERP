using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.HR;

/// <summary>
/// Nightly host for the orientation audience-rule sweep (round 4, lane I3).
///
/// <para>All logic lives in <see cref="IOrientationEnrollmentTriggerService.RunSweepForTenantAsync"/>
/// so the HR run-now endpoint (<c>POST api/orientation-programs/triggers/run</c>) exercises exactly
/// the code path this host does — the host/processor split every HR sweep here uses.</para>
///
/// <para><b>What it is for.</b> The event hooks (hire, movement, publish) fire the rules whose day
/// has already come. This fires everything else: <c>Scheduled</c> rules, a hire rule with a delay,
/// a movement whose effective date has arrived, and any event whose hook failed. Daily is enough —
/// every rule it runs is date-granular, and every write is de-duplicated against the enrollments
/// that already exist, so cadence changes latency and never the outcome.</para>
///
/// <para>⚠ <b>Registration is the part that gets forgotten</b> — see
/// <c>ServiceCollectionExtensions</c>. Two HR sweeps in this repo were written, tested and
/// registered nowhere. If automatic enrollments stop appearing, check that line first, then the
/// log for "Orientation triggers (".</para>
/// </summary>
public sealed class OrientationTriggerBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrientationTriggerBackgroundService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(24);

    public OrientationTriggerBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<OrientationTriggerBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Orientation Trigger Background Service is starting");

        try
        {
            // Staggered behind the other HR sweeps so a cold start does not run them all at once.
            await Task.Delay(TimeSpan.FromMinutes(13), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during the orientation trigger sweep");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Orientation Trigger Background Service is stopping due to cancellation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in Orientation Trigger Background Service");
            throw;
        }
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var lockService = scope.ServiceProvider.GetRequiredService<IDistributedLockService>();

        await using var leader = await lockService.TryAcquireAsync(
            lockName: "bg:orientation-trigger-sweep",
            leaseDuration: TimeSpan.FromMinutes(30),
            cancellationToken: cancellationToken);

        if (leader == null)
        {
            _logger.LogDebug("Skipping orientation trigger sweep (lock not acquired)");
            return;
        }

        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var tenants = await unitOfWork.Repository<Tenant>()
            .GetQueryable(t => !t.IsDeleted)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        foreach (var tenantId in tenants)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // A scope per tenant: the sweep stages rows on the scoped unit of work, and one
            // tenant's failed save must not leave its entities in the next tenant's tracker.
            using var tenantScope = _serviceProvider.CreateScope();
            try
            {
                var triggers = tenantScope.ServiceProvider.GetRequiredService<IOrientationEnrollmentTriggerService>();
                await triggers.RunSweepForTenantAsync(tenantId, "Scheduled", triggeredByUserId: null,
                    preview: false, cancellationToken);
            }
            catch (Exception ex)
            {
                // One tenant's failure must not starve the rest.
                _logger.LogError(ex, "Orientation trigger sweep failed for tenant {TenantId}", tenantId);
            }
        }
    }
}
