using ErpSystem.Core.Entities.Identity;
using ErpSystem.Core.Interfaces.Identity;
using ErpSystem.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Identity;

public sealed class HrIdentityReconciliationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HrIdentityReconciliationBackgroundService> _logger;

    public HrIdentityReconciliationBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<HrIdentityReconciliationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = Math.Clamp(
            _configuration.GetValue("HrIdentityReconciliation:IntervalMinutes", 5),
            1,
            1440);
        var interval = TimeSpan.FromMinutes(intervalMinutes);
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                await ExecuteCycleAsync(intervalMinutes, stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(exception, "Scheduled HR/Identity reconciliation cycle failed.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    internal async Task ExecuteCycleAsync(int intervalMinutes, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IHrIdentityReconciliationService>();
        var tenantIds = await (
            from user in db.Users.IgnoreQueryFilters().AsNoTracking()
            join employee in db.Employees.IgnoreQueryFilters().AsNoTracking()
                on user.EmployeeId equals employee.Id
            where !employee.IsDeleted && user.TenantId == employee.TenantId
            select user.TenantId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var bucket = DateTime.UtcNow.Ticks / TimeSpan.FromMinutes(intervalMinutes).Ticks;

        foreach (var tenantId in tenantIds)
        {
            try
            {
                await service.RunAsync(
                    tenantId,
                    null,
                    HrIdentityReconciliationTrigger.Scheduled,
                    $"scheduled:{tenantId:N}:{bucket}",
                    cancellationToken: cancellationToken);
            }
            catch (DbUpdateException exception) when (
                exception.InnerException is SqlException { Number: 2601 or 2627 })
            {
                // The unique tenant/idempotency key is the distributed lock when more than one API host is running.
                _logger.LogInformation(
                    exception,
                    "Another host already started scheduled HR/Identity reconciliation for tenant {TenantId}, bucket {Bucket}.",
                    tenantId,
                    bucket);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Scheduled HR/Identity reconciliation failed for tenant {TenantId}.",
                    tenantId);
            }
        }
    }
}
