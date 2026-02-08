using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Api.Services.Maintenance;

/// <summary>
/// Background service that publishes due-soon / overdue compliance reminder events for fleet vehicles.
/// Actual delivery is handled by the unified Notification Topics + outbox dispatcher.
/// </summary>
public sealed class FleetComplianceReminderBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FleetComplianceReminderBackgroundService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(6);

    public FleetComplianceReminderBackgroundService(IServiceProvider serviceProvider, ILogger<FleetComplianceReminderBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Fleet Compliance Reminder Background Service is starting");

        try
        {
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during fleet compliance reminder processing");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Fleet Compliance Reminder Background Service is stopping due to cancellation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in Fleet Compliance Reminder Background Service");
            throw;
        }
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var appEventBus = scope.ServiceProvider.GetRequiredService<IAppEventBus>();

        var today = DateTime.UtcNow.Date;

        var tenants = await unitOfWork.Repository<Tenant>()
            .GetQueryable(t => !t.IsDeleted)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        foreach (var tenantId in tenants)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var settings = await unitOfWork.Repository<MaintenanceSettings>()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId);

            var dueSoonDays = settings?.FleetComplianceDueSoonDays ?? 7;
            var dueSoonCutoff = today.AddDays(Math.Max(0, dueSoonDays));

            var complianceRepo = unitOfWork.Repository<FleetComplianceItem>();

            var q = complianceRepo.GetQueryable(i =>
                    i.TenantId == tenantId &&
                    !i.IsDeleted &&
                    i.IsCritical)
                .Include(i => i.VehicleAsset);

            // Due soon: [today, cutoff]
            var dueSoonItems = await q
                .Where(i => i.ExpiryDate.Date >= today && i.ExpiryDate.Date <= dueSoonCutoff)
                .ToListAsync(cancellationToken);

            foreach (var item in dueSoonItems)
            {
                if (item.LastDueSoonReminderSentAt.HasValue && item.LastDueSoonReminderSentAt.Value.Date >= today)
                    continue;

                item.LastDueSoonReminderSentAt = DateTime.UtcNow;
                await complianceRepo.UpdateAsync(item);

                await appEventBus.PublishAsync(new EntityActivityEvent
                {
                    TenantId = tenantId,
                    EntityType = "FleetCompliance",
                    Activity = "ComplianceDueSoon",
                    Audience = "Internal",
                    EntityId = item.Id,
                    TriggeredByUserId = null,
                    Data = new Dictionary<string, object>
                    {
                        ["VehicleAssetId"] = item.VehicleAssetId,
                        ["VehicleName"] = item.VehicleAsset?.Name ?? string.Empty,
                        ["ComplianceType"] = item.ComplianceType,
                        ["ExpiryDate"] = item.ExpiryDate,
                        ["DaysToExpiry"] = (item.ExpiryDate.Date - today).Days
                    }
                }, cancellationToken);
            }

            // Overdue: < today
            var overdueItems = await q
                .Where(i => i.ExpiryDate.Date < today)
                .ToListAsync(cancellationToken);

            foreach (var item in overdueItems)
            {
                if (item.LastOverdueReminderSentAt.HasValue && item.LastOverdueReminderSentAt.Value.Date >= today)
                    continue;

                item.LastOverdueReminderSentAt = DateTime.UtcNow;
                await complianceRepo.UpdateAsync(item);

                await appEventBus.PublishAsync(new EntityActivityEvent
                {
                    TenantId = tenantId,
                    EntityType = "FleetCompliance",
                    Activity = "ComplianceOverdue",
                    Audience = "Internal",
                    EntityId = item.Id,
                    TriggeredByUserId = null,
                    Data = new Dictionary<string, object>
                    {
                        ["VehicleAssetId"] = item.VehicleAssetId,
                        ["VehicleName"] = item.VehicleAsset?.Name ?? string.Empty,
                        ["ComplianceType"] = item.ComplianceType,
                        ["ExpiryDate"] = item.ExpiryDate,
                        ["DaysOverdue"] = (today - item.ExpiryDate.Date).Days
                    }
                }, cancellationToken);
            }

            await unitOfWork.SaveChangesAsync();
        }
    }
}

