using System.Security.Claims;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Estate;

public sealed class EstateRecurringBillingBackgroundService(
    IServiceProvider services,
    IHttpContextAccessor httpContextAccessor,
    ILogger<EstateRecurringBillingBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
        do
        {
            try
            {
                using var scope = services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var tenants = await db.Tenants.AsNoTracking()
                    .Where(item => !item.IsDeleted)
                    .Select(item => item.Id)
                    .ToListAsync(stoppingToken);
                foreach (var tenantId in tenants)
                {
                    stoppingToken.ThrowIfCancellationRequested();
                    try
                    {
                        using var tenantScope = services.CreateScope();
                        var previous = httpContextAccessor.HttpContext;
                        try
                        {
                            httpContextAccessor.HttpContext = new DefaultHttpContext
                            {
                                User = new ClaimsPrincipal(new ClaimsIdentity(
                                [
                                    new Claim("tenant_id", tenantId.ToString()),
                                    new Claim(ClaimTypes.Name, "Scheduled billing")
                                ], "ScheduledBilling"))
                            };
                            var runner = tenantScope.ServiceProvider.GetRequiredService<EstateRecurringBillingService>();
                            var result = await runner.RunForTenantAsync(tenantId, stoppingToken);
                            if (result.Failures > 0)
                                logger.LogWarning("Estate recurring billing had {Failures} failures for tenant {TenantId}",
                                    result.Failures, tenantId);
                        }
                        finally
                        {
                            httpContextAccessor.HttpContext = previous;
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogError(ex, "Estate recurring billing failed for tenant {TenantId}", tenantId);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Estate recurring billing sweep failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
