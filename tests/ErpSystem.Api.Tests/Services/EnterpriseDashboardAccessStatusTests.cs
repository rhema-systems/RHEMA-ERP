using ErpSystem.Api.Services;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ErpSystem.Api.Tests.Services;

public sealed class EnterpriseDashboardAccessStatusTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Expected_scope_denial_is_restricted_without_exposing_protected_metrics(bool inventoryDenial)
    {
        Exception denial = inventoryDenial
            ? new InventoryAnalyticsAuthorizationException("No assigned inventory location.")
            : new ProcurementAccessAuthorizationException("No assigned report capability.");
        using var provider = CreateProvider(denial);
        var service = CreateService(provider);

        var dashboard = await service.GetEnterpriseDashboardAsync();

        var status = Assert.Single(dashboard.ModuleStatus,
            value => value.Module == "Procurement and Inventory Management");
        Assert.False(status.Available);
        Assert.True(status.AccessRestricted);
        Assert.Null(dashboard.ProcurementInventoryManagement);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Other_failures_remain_visible_as_outages(bool databaseFailure)
    {
        Exception failure = databaseFailure
            ? new InvalidOperationException("Database query failed.")
            : new UnauthorizedAccessException("Unexpected authorization failure.");
        using var provider = CreateProvider(failure);
        var service = CreateService(provider);

        var dashboard = await service.GetEnterpriseDashboardAsync();

        var status = Assert.Single(dashboard.ModuleStatus,
            value => value.Module == "Procurement and Inventory Management");
        Assert.False(status.Available);
        Assert.False(status.AccessRestricted);
        Assert.Equal(failure.Message, status.Error);
        Assert.Null(dashboard.ProcurementInventoryManagement);
    }

    private static ServiceProvider CreateProvider(Exception failure) =>
        new ServiceCollection()
            .AddScoped<ProcurementInventoryManagementDashboardService>(_ => throw failure)
            .BuildServiceProvider();

    private static EnterpriseDashboardService CreateService(ServiceProvider provider) => new(
        provider.GetRequiredService<IServiceScopeFactory>(),
        NullLogger<EnterpriseDashboardService>.Instance);
}
