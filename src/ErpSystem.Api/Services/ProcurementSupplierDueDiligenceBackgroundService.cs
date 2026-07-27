using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services;

public sealed class ProcurementSupplierDueDiligenceBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ProcurementSupplierDueDiligenceBackgroundService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    public ProcurementSupplierDueDiligenceBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ProcurementSupplierDueDiligenceBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
            using var timer = new PeriodicTimer(Interval);
            do
            {
                await ProcessAsync(stoppingToken);
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Supplier due-diligence background service stopped.");
        }
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var lockService = scope.ServiceProvider.GetRequiredService<IDistributedLockService>();
        await using var leader = await lockService.TryAcquireAsync(
            "bg:procurement-supplier-due-diligence",
            TimeSpan.FromMinutes(30),
            cancellationToken);
        if (leader is null)
        {
            _logger.LogDebug(
                "Skipping supplier due-diligence expiry because another node holds the lock.");
            return;
        }

        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var service = scope.ServiceProvider
            .GetRequiredService<IProcurementSupplierDueDiligenceService>();
        var tenantIds = await unitOfWork.Repository<Tenant>()
            .GetQueryable(item => !item.IsDeleted)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var tenantId in tenantIds)
        {
            try
            {
                await service.ProcessExpiryAsync(tenantId, now, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(
                    exception,
                    "Supplier due-diligence expiry failed for tenant {TenantId}.",
                    tenantId);
            }
        }
    }
}
