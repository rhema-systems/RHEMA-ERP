using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services;

public sealed class ProcurementSupplierAvlBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ProcurementSupplierAvlBackgroundService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    public ProcurementSupplierAvlBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ProcurementSupplierAvlBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken);
            using var timer = new PeriodicTimer(Interval);
            do
            {
                await ProcessAsync(stoppingToken);
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Supplier AVL background service stopped.");
        }
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var lockService = scope.ServiceProvider.GetRequiredService<IDistributedLockService>();
        await using var leader = await lockService.TryAcquireAsync(
            "bg:procurement-supplier-avl",
            TimeSpan.FromMinutes(30),
            cancellationToken);
        if (leader is null) return;
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var service = scope.ServiceProvider.GetRequiredService<IProcurementSupplierAvlService>();
        var tenantIds = await unitOfWork.Repository<Tenant>()
            .GetQueryable(item => !item.IsDeleted)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        foreach (var tenantId in tenantIds)
        {
            try
            {
                await service.ProcessExpiryAsync(tenantId, DateTime.UtcNow, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception,
                    "Supplier AVL expiry failed for tenant {TenantId}.", tenantId);
            }
        }
    }
}
