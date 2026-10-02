using ErpSystem.Core.Entities;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using ErpSystem.Data.Services;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services;

public sealed class LegacyDashboardRealDataTests
{
    [Fact]
    public async Task Legacy_dashboard_returns_explicit_empty_or_unavailable_state_instead_of_samples()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"legacy-dashboard-{Guid.NewGuid()}")
            .Options;
        await using var context = new ApplicationDbContext(options);
        context.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Code = "DASH",
            Name = "Dashboard Tenant",
            Status = TenantStatus.Active,
            BaseCurrency = "GHS"
        });
        await context.SaveChangesAsync();
        var service = new DashboardService(
            context,
            Mock.Of<IHubNotificationService>(),
            NullLogger<DashboardService>.Instance);

        var dashboard = await service.GetDashboardDataAsync(tenantId.ToString());

        Assert.Empty(dashboard.Notifications);
        Assert.Null(dashboard.Metrics.SystemUptime);
        Assert.Null(dashboard.SystemStatus.CpuUsage);
        Assert.Null(dashboard.SystemStatus.MemoryUsage);
        Assert.Null(dashboard.SystemStatus.DiskUsage);
        Assert.Contains("telemetry unavailable", dashboard.SystemStatus.Status, StringComparison.OrdinalIgnoreCase);
        Assert.Single(dashboard.SystemStatus.Services, value => value.Name == "Database");
    }
}
