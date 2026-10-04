using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using ErpSystem.Core.DTOs.Dashboard;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers;

public sealed class DashboardAuthorizationIntegrationTests
{
    [Fact]
    public async Task Dashboard_only_user_can_open_endpoint_without_receiving_finance_metrics()
    {
        using var factory = CreateFactory(financeRead: false, out var ledger);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dashboard/enterprise?startDate=2026-07-01&endDate=2026-07-31");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dashboard = await response.Content.ReadFromJsonAsync<EnterpriseDashboardDto>();
        Assert.NotNull(dashboard);
        Assert.Null(dashboard!.FinanceOverview);
        var finance = Assert.Single(dashboard.ModuleStatus, value => value.Module == "Finance");
        Assert.True(finance.AccessRestricted);
        Assert.False(finance.Available);
        Assert.Null(dashboard.Crm);
        Assert.All(
            new[] { "CRM", "Procurement Queues", "Inventory Queues" },
            module =>
            {
                var status = Assert.Single(dashboard.ModuleStatus, value => value.Module == module);
                Assert.True(status.AccessRestricted);
                Assert.False(status.Available);
            });
        Assert.Equal(0, dashboard.OperationalQueues.OpenPurchaseOrderCount);
        Assert.Equal(0, dashboard.OperationalQueues.PendingInventoryIssueCount);
        ledger.Verify(value => value.GetFinanceDashboardAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task Dashboard_user_with_finance_read_receives_authoritative_finance_projection()
    {
        using var factory = CreateFactory(financeRead: true, out var ledger);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dashboard/enterprise?startDate=2026-07-01&endDate=2026-07-31");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dashboard = await response.Content.ReadFromJsonAsync<EnterpriseDashboardDto>();
        Assert.NotNull(dashboard?.FinanceOverview);
        Assert.Equal(725m, dashboard!.FinanceOverview!.Kpis.NetProfit);
        var finance = Assert.Single(dashboard.ModuleStatus, value => value.Module == "Finance");
        Assert.True(finance.Available);
        Assert.False(finance.AccessRestricted);
        ledger.Verify(value => value.GetFinanceDashboardAsync(
            new DateTime(2026, 7, 1), new DateTime(2026, 7, 31)), Times.Once);
    }

    private static WebApplicationFactory<Program> CreateFactory(bool financeRead, out Mock<IGeneralLedgerService> ledger)
    {
        ledger = new Mock<IGeneralLedgerService>(MockBehavior.Strict);
        if (financeRead)
        {
            ledger.Setup(value => value.GetFinanceDashboardAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new FinanceDashboardDto
                {
                    CurrencyCode = "GHS",
                    CurrencySymbol = "GH₵",
                    CurrencyDecimalPlaces = 2,
                    Kpis = new FinanceDashboardKpisDto { NetProfit = 725m }
                });
        }

        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(value => value.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(), null, It.IsAny<string>()))
            .ReturnsAsync((ClaimsPrincipal _, object? _, string policy) =>
                policy == FinancePermissions.ViewFinance && financeRead
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());

        var tenantSettings = new Mock<ITenantSettingsService>();
        tenantSettings.Setup(value => value.GetBaseCurrencyReferenceAsync()).ReturnsAsync(new BaseCurrencyReferenceDto
        {
            CurrencyCode = "GHS",
            CurrencyName = "Ghana Cedi",
            CurrencySymbol = "GH₵",
            DecimalPlaces = 2
        });

        var ledgerService = ledger.Object;
        var projects = new Mock<IProjectService>();
        projects.Setup(value => value.GetDashboardAsync()).ReturnsAsync(new ProjectDashboardDto());
        var maintenanceAnalytics = new Mock<IMaintenanceAnalyticsService>();
        maintenanceAnalytics.Setup(value => value.GetDashboardDataAsync()).ReturnsAsync(new MaintenanceDashboardDto());
        maintenanceAnalytics.Setup(value => value.GetWorkOrderTrendsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new WorkOrderTrendsDto());
        var workOrders = new Mock<IWorkOrderService>();
        workOrders.Setup(value => value.GetWorkOrderMetricsAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(new WorkOrderMetricsDto());
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("CandidatePortal:PortalUrl", "https://candidate.test/");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IPolicyEvaluator>();
                services.RemoveAll<IAuthorizationService>();
                services.RemoveAll<IGeneralLedgerService>();
                services.RemoveAll<ITenantSettingsService>();
                services.RemoveAll<IProjectService>();
                services.RemoveAll<IMaintenanceAnalyticsService>();
                services.RemoveAll<IWorkOrderService>();
                services.AddSingleton<IPolicyEvaluator>(new DashboardPolicyEvaluator());
                services.AddSingleton(authorization.Object);
                services.AddSingleton(ledgerService);
                services.AddSingleton(tenantSettings.Object);
                services.AddSingleton(projects.Object);
                services.AddSingleton(maintenanceAnalytics.Object);
                services.AddSingleton(workOrders.Object);
            });
        });
    }

    private sealed class DashboardPolicyEvaluator : IPolicyEvaluator
    {
        private static readonly ClaimsPrincipal Principal = new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, "dashboard.user@erp.local"),
            new Claim("tenant_id", Guid.NewGuid().ToString())
        ], "DashboardTest"));

        public Task<AuthenticateResult> AuthenticateAsync(AuthorizationPolicy policy, HttpContext context) =>
            Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(Principal, "DashboardTest")));

        public Task<PolicyAuthorizationResult> AuthorizeAsync(
            AuthorizationPolicy policy,
            AuthenticateResult authenticationResult,
            HttpContext context,
            object? resource) => Task.FromResult(PolicyAuthorizationResult.Success());
    }
}
