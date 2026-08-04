using ErpSystem.Api.Controllers;
using ErpSystem.Api.Services;
using ErpSystem.Core.DTOs.Dashboard;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers;

public sealed class DashboardControllerDateRangeTests
{
    [Fact]
    public async Task Enterprise_dashboard_rejects_a_partial_date_range()
    {
        var controller = CreateController();

        var result = await controller.GetEnterpriseDashboard(new DateTime(2026, 1, 1), null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("startDate and endDate must be supplied together.", badRequest.Value);
    }

    [Fact]
    public async Task Enterprise_dashboard_rejects_an_inverted_date_range()
    {
        var controller = CreateController();

        var result = await controller.GetEnterpriseDashboard(
            new DateTime(2026, 4, 1),
            new DateTime(2026, 3, 31));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("startDate cannot be later than endDate.", badRequest.Value);
    }

    [Fact]
    public async Task Enterprise_dashboard_rejects_a_range_longer_than_ten_years()
    {
        var controller = CreateController();

        var result = await controller.GetEnterpriseDashboard(
            new DateTime(2015, 1, 1),
            new DateTime(2026, 1, 2));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("The dashboard date range cannot exceed 10 years.", badRequest.Value);
    }

    [Fact]
    public async Task Enterprise_dashboard_rejects_a_location_without_a_warehouse()
    {
        var controller = CreateController();

        var result = await controller.GetEnterpriseDashboard(
            new DateTime(2026, 1, 1),
            new DateTime(2026, 3, 31),
            locationId: Guid.NewGuid());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("warehouseId is required when locationId is supplied.", badRequest.Value);
    }

    [Fact]
    public async Task Enterprise_dashboard_returns_the_normalized_selected_range()
    {
        var controller = CreateController();

        var result = await controller.GetEnterpriseDashboard(
            new DateTime(2026, 1, 1),
            new DateTime(2026, 3, 31));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dashboard = Assert.IsType<EnterpriseDashboardDto>(ok.Value);
        Assert.Equal(new DateTime(2026, 1, 1), dashboard.RangeStartDate.Date);
        Assert.Equal(new DateTime(2026, 3, 31), dashboard.RangeEndDate.Date);
    }

    private static DashboardController CreateController()
    {
        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory
            .Setup(factory => factory.CreateScope())
            .Returns(() => new ServiceCollection().BuildServiceProvider().CreateScope());

        var enterpriseDashboard = new EnterpriseDashboardService(
            scopeFactory.Object,
            NullLogger<EnterpriseDashboardService>.Instance);

        return new DashboardController(
            Mock.Of<IDashboardService>(),
            enterpriseDashboard,
            Mock.Of<ICurrentUserService>(),
            NullLogger<DashboardController>.Instance);
    }
}
