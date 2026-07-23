using ErpSystem.Core.Entities;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services;

public sealed class ProcurementCalendarBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ProcurementCalendarBackgroundService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(6);

    public ProcurementCalendarBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ProcurementCalendarBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
            using var timer = new PeriodicTimer(_interval);
            do
            {
                await ProcessAsync(stoppingToken);
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Procurement calendar background service stopped.");
        }
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var lockService = scope.ServiceProvider.GetRequiredService<IDistributedLockService>();
        await using var leader = await lockService.TryAcquireAsync(
            "bg:procurement-calendar", TimeSpan.FromMinutes(30), cancellationToken);
        if (leader is null)
        {
            _logger.LogDebug("Skipping procurement calendar processing because another node holds the lock.");
            return;
        }

        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var processor = scope.ServiceProvider.GetRequiredService<IProcurementCalendarProcessor>();
        var tenantIds = await unitOfWork.Repository<Tenant>().GetQueryable(item => !item.IsDeleted)
            .Select(item => item.Id).ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var tenantId in tenantIds)
        {
            try
            {
                await processor.ProcessTenantAsync(tenantId, now, null, ProcurementCalendarRunTrigger.Scheduled,
                    null, "Procurement calendar scheduler", "Scheduled calendar generation, reminder, and escalation run.",
                    $"scheduler-{tenantId:N}-{now:yyyyMMddHH}", cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Procurement calendar processing failed for tenant {TenantId}", tenantId);
            }
        }
    }
}
