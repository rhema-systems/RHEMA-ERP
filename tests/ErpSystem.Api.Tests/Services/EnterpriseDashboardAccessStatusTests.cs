using System.Security.Claims;
using ErpSystem.Api.Services;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services;

public sealed class EnterpriseDashboardAccessStatusTests
{
    [Fact]
    public async Task SuperAdmin_with_tenant_scoped_dashboard_read_sees_module_metrics_without_module_claims()
    {
        using var provider = await CreateModuleProvider(superAdmin: true, tenantAccess: true);
        var service = CreateService(provider);
        var dashboard = await service.GetEnterpriseDashboardAsync(
            DateTime.UtcNow.Date.AddDays(-1), DateTime.UtcNow.Date.AddDays(1));

        Assert.Equal(1, dashboard.Crm?.OpenOpportunityCount);
        Assert.True(Assert.Single(dashboard.ModuleStatus, value => value.Module == "CRM").Available);
        Assert.True(Assert.Single(dashboard.ModuleStatus, value => value.Module == "Procurement Queues").Available);
        Assert.True(Assert.Single(dashboard.ModuleStatus, value => value.Module == "Inventory Queues").Available);
        Assert.Equal(1, dashboard.OperationalQueues.PendingInventoryIssueCount);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Module_claim_gate_remains_when_SuperAdmin_lacks_tenant_access_or_actor_is_ordinary(
        bool superAdmin,
        bool tenantAccess)
    {
        using var provider = await CreateModuleProvider(superAdmin, tenantAccess);
        var service = CreateService(provider);
        var dashboard = await service.GetEnterpriseDashboardAsync(
            DateTime.UtcNow.Date.AddDays(-1), DateTime.UtcNow.Date.AddDays(1));

        Assert.Null(dashboard.Crm);
        foreach (var module in new[] { "CRM", "Procurement Queues", "Inventory Queues" })
        {
            var status = Assert.Single(dashboard.ModuleStatus, value => value.Module == module);
            Assert.False(status.Available);
            Assert.True(status.AccessRestricted);
        }
    }

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
        Assert.Equal("Procurement and inventory management metrics are unavailable", status.Error);
        Assert.DoesNotContain("Database query failed", status.Error);
        Assert.DoesNotContain("Unexpected authorization failure", status.Error);
        Assert.Null(dashboard.ProcurementInventoryManagement);
    }

    [Fact]
    public async Task Finance_metrics_are_omitted_when_the_actor_lacks_finance_read_permission()
    {
        using var provider = CreateFinanceProvider(allowed: false, out var ledger);
        var service = CreateService(provider);

        var dashboard = await service.GetEnterpriseDashboardAsync(
            new DateTime(2026, 7, 1),
            new DateTime(2026, 9, 30));

        var status = Assert.Single(dashboard.ModuleStatus, value => value.Module == "Finance");
        Assert.False(status.Available);
        Assert.True(status.AccessRestricted);
        Assert.Null(status.Error);
        Assert.Null(dashboard.FinanceOverview);
        ledger.Verify(
            service => service.GetFinanceDashboardAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()),
            Times.Never);
    }

    [Fact]
    public async Task Finance_metrics_use_the_selected_range_and_configured_reporting_currency()
    {
        var expected = new FinanceDashboardDto
        {
            CurrencyCode = "GHS",
            CurrencySymbol = "GH₵",
            CurrencyDecimalPlaces = 2,
            Kpis = new FinanceDashboardKpisDto { Revenue = 1250m }
        };
        using var provider = CreateFinanceProvider(allowed: true, out var ledger, result: expected);
        var service = CreateService(provider);
        var start = new DateTime(2026, 7, 1);
        var end = new DateTime(2026, 9, 30);

        var dashboard = await service.GetEnterpriseDashboardAsync(start, end);

        var status = Assert.Single(dashboard.ModuleStatus, value => value.Module == "Finance");
        Assert.True(status.Available);
        Assert.False(status.AccessRestricted);
        Assert.Same(expected, dashboard.FinanceOverview);
        Assert.Equal("GHS", dashboard.ReportingCurrency.CurrencyCode);
        Assert.Equal(2, dashboard.ReportingCurrency.DecimalPlaces);
        ledger.Verify(service => service.GetFinanceDashboardAsync(start, end), Times.Once);
    }

    [Theory]
    [InlineData(150, 100, 50)]
    [InlineData(75, 100, -25)]
    [InlineData(0, 0, 0)]
    public void Finance_comparison_is_calculated_against_the_previous_period(
        decimal current,
        decimal previous,
        decimal expected)
    {
        Assert.Equal(expected, GeneralLedgerService.CalculateDashboardChange(current, previous));
    }

    [Fact]
    public void Finance_comparison_does_not_invent_a_percentage_without_a_baseline()
    {
        Assert.Null(GeneralLedgerService.CalculateDashboardChange(100m, 0m));
    }

    private static ServiceProvider CreateProvider(Exception failure)
    {
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(service => service.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(), null, It.IsAny<string>()))
            .ReturnsAsync(AuthorizationResult.Success());
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()) },
                authenticationType: "Test"))
        };

        return new ServiceCollection()
            .AddSingleton(authorization.Object)
            .AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = httpContext })
            .AddScoped<ProcurementInventoryManagementDashboardService>(_ => throw failure)
            .BuildServiceProvider();
    }

    private static async Task<ServiceProvider> CreateModuleProvider(bool superAdmin, bool tenantAccess)
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("tenant_id", tenantId.ToString())
        };
        if (superAdmin)
            claims.Add(new Claim(ClaimTypes.Role, Constants.Roles.SuperAdmin));

        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(service => service.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(), null, It.IsAny<string>()))
            .ReturnsAsync((ClaimsPrincipal _, object? _, string policy) =>
                policy == "dashboard.read" && tenantAccess
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());

        var actor = new Mock<ICurrentUserProvider>();
        actor.SetupGet(value => value.TenantId).Returns(tenantId);
        actor.SetupGet(value => value.UserId).Returns(userId);

        var provider = new ServiceCollection()
            .AddSingleton(authorization.Object)
            .AddSingleton<ICurrentUserProvider>(actor.Object)
            .AddSingleton<IProcurementAccessControlService>(new Mock<IProcurementAccessControlService>().Object)
            .AddSingleton<IHttpContextAccessor>(new FixedHttpContextAccessor
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"))
                }
            })
            .AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase($"enterprise-module-access-{tenantId:N}"))
            .AddScoped<EnterpriseDashboardProjectionService>()
            .BuildServiceProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Leads.Add(new Lead
            {
                Id = Guid.NewGuid(), TenantId = tenantId, FirstName = "Ama", LastName = "Mensah"
            });
            db.Opportunities.Add(new Opportunity
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Name = "Tenant opportunity",
                Stage = "Qualification", ExpectedCloseDate = DateTime.UtcNow.Date
            });
            db.InventoryRequisitions.Add(new InventoryRequisition
            {
                Id = Guid.NewGuid(), TenantId = tenantId,
                WarehouseId = Guid.NewGuid(), RequestedById = Guid.NewGuid(),
                RequisitionNumber = "REQ-SUPERADMIN-VISIBLE",
                RequestDate = DateTime.UtcNow.Date,
                Status = RequisitionStatus.Approved
            });
            await db.SaveChangesAsync();
        }

        return provider;
    }

    private sealed class FixedHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }

    private static ServiceProvider CreateFinanceProvider(
        bool allowed,
        out Mock<IGeneralLedgerService> ledger,
        FinanceDashboardDto? result = null)
    {
        ledger = new Mock<IGeneralLedgerService>(MockBehavior.Strict);
        if (allowed)
        {
            ledger
                .Setup(service => service.GetFinanceDashboardAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(result ?? new FinanceDashboardDto());
        }

        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                null,
                It.IsAny<string>()))
            .ReturnsAsync((ClaimsPrincipal _, object? _, string policy) =>
                policy != FinancePermissions.ViewFinance || allowed
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());

        var tenantSettings = new Mock<ITenantSettingsService>();
        tenantSettings
            .Setup(service => service.GetBaseCurrencyReferenceAsync())
            .ReturnsAsync(new BaseCurrencyReferenceDto
            {
                CurrencyCode = "GHS",
                CurrencyName = "Ghana Cedi",
                CurrencySymbol = "GH₵",
                DecimalPlaces = 2
            });

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()) },
                authenticationType: "Test"))
        };

        return new ServiceCollection()
            .AddSingleton(authorization.Object)
            .AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = httpContext })
            .AddSingleton(tenantSettings.Object)
            .AddSingleton(ledger.Object)
            .BuildServiceProvider();
    }

    private static EnterpriseDashboardService CreateService(ServiceProvider provider) => new(
        provider.GetRequiredService<IServiceScopeFactory>(),
        NullLogger<EnterpriseDashboardService>.Instance);
}
