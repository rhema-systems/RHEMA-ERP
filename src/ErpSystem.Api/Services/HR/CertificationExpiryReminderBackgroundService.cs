using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.HR;

/// <summary>
/// Hosts the certification-expiry sweep: one pass a day, per tenant, under a lease so a fleet
/// raises each reminder once. The identification-expiry host, one family over (demo feedback
/// round 2, lane C2).
/// </summary>
/// <remarks>
/// ⚠ Registered together with <see cref="ICertificationExpiryReminderService"/> on purpose — two
/// HR engines once existed with endpoints and never ran, because nothing hosted them.
/// </remarks>
public sealed class CertificationExpiryReminderBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CertificationExpiryReminderBackgroundService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(24);

    public CertificationExpiryReminderBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<CertificationExpiryReminderBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Certification Expiry Reminder Background Service is starting");

        try
        {
            // Staggered clear of the other HR sweeps on a cold start; 29 minutes puts this after
            // identification expiry's 23.
            await Task.Delay(TimeSpan.FromMinutes(29), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during certification expiry reminder processing");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Certification Expiry Reminder Background Service is stopping due to cancellation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in Certification Expiry Reminder Background Service");
            throw;
        }
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var lockService = scope.ServiceProvider.GetRequiredService<IDistributedLockService>();

        await using var leader = await lockService.TryAcquireAsync(
            lockName: "bg:certification-expiry-reminders",
            leaseDuration: TimeSpan.FromMinutes(30),
            cancellationToken: cancellationToken);

        if (leader == null)
        {
            _logger.LogDebug("Skipping certification expiry run (lock not acquired)");
            return;
        }

        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var reminderService = scope.ServiceProvider.GetRequiredService<ICertificationExpiryReminderService>();

        var tenants = await unitOfWork.Repository<Tenant>()
            .GetQueryable(t => !t.IsDeleted)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        foreach (var tenantId in tenants)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await reminderService.RunSweepForTenantAsync(tenantId, "Scheduled", triggeredByUserId: null, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Certification expiry sweep failed for tenant {TenantId}", tenantId);
            }
        }
    }
}
