using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services;

public sealed class ProcurementTenderClosingBackgroundService(
    IServiceScopeFactory scopeFactory, ILogger<ProcurementTenderClosingBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        try
        {
            do { await RunOnceAsync(stoppingToken); }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var locks = services.GetRequiredService<IDistributedLockService>();
            await using var leader = await locks.TryAcquireAsync("bg:procurement-tender-closing", TimeSpan.FromMinutes(5), cancellationToken);
            if (leader is null) return;
            var unit = services.GetRequiredService<IUnitOfWork>();
            var tenants = await unit.Repository<Tenant>().GetQueryable().IgnoreQueryFilters()
                .Where(tenant => !tenant.IsDeleted).Select(tenant => tenant.Id).ToListAsync(cancellationToken);
            foreach (var tenantId in tenants)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Separate scopes isolate tracking and failed transactions between tenants.
                await using var tenantScope = scopeFactory.CreateAsyncScope();
                try
                {
                    var count = await tenantScope.ServiceProvider.GetRequiredService<ProcurementTenderClosingProcessor>()
                        .ProcessTenantAsync(tenantId, DateTime.UtcNow, cancellationToken);
                    if (count > 0) logger.LogInformation("Automatically closed {Count} tenders for tenant {TenantId}", count, tenantId);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(exception, "Automatic tender closing failed for tenant {TenantId}", tenantId);
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Automatic tender closing cycle failed");
        }
    }
}
