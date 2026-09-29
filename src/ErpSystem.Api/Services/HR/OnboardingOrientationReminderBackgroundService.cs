using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.HR;

/// <summary>
/// Daily host for the orientation & onboarding reminder engine (round 4, lane K).
///
/// All sweep logic lives in <see cref="IOnboardingOrientationReminderService"/>, so the run-now
/// endpoint (POST api/orientation-reminders/run) exercises exactly the code path this host does —
/// the same host/processor split every HR reminder engine uses.
///
/// ⚠ Registered with <c>AddHostedService</c> in the same change as this class (lane K5). Two HR sweeps
/// in this codebase turned out never to have run because that line was missing; a run row with
/// Trigger "Scheduled" is the proof this one does.
///
/// Daily: the finest thing swept is a date, and a reminder about a date does not become more useful
/// for being repeated every hour. Per-item claims mean cadence affects latency only, never duplicates.
/// </summary>
public sealed class OnboardingOrientationReminderBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OnboardingOrientationReminderBackgroundService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(24);

    public OnboardingOrientationReminderBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<OnboardingOrientationReminderBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Orientation & Onboarding Reminder Background Service is starting");

        try
        {
            // Staggered: the other HR sweeps start at 3–29 minutes; 19 was free.
            await Task.Delay(TimeSpan.FromMinutes(19), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during orientation & onboarding reminder processing");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Orientation & Onboarding Reminder Background Service is stopping due to cancellation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in Orientation & Onboarding Reminder Background Service");
            throw;
        }
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var lockService = scope.ServiceProvider.GetRequiredService<IDistributedLockService>();

        await using var leader = await lockService.TryAcquireAsync(
            lockName: "bg:onboarding-orientation-reminders",
            leaseDuration: TimeSpan.FromMinutes(30),
            cancellationToken: cancellationToken);

        if (leader == null)
        {
            _logger.LogDebug("Skipping orientation & onboarding reminder run (lock not acquired)");
            return;
        }

        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var reminderService = scope.ServiceProvider.GetRequiredService<IOnboardingOrientationReminderService>();

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
                _logger.LogError(ex, "Orientation & onboarding reminder sweep failed for tenant {TenantId}", tenantId);
            }
        }
    }
}
