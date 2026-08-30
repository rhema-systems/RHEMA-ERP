using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Procurement;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class ProcurementPlansFiscalYearsControllerTests
{
    [Fact]
    public async Task PlanningFiscalYearsUseTenantScopedFinanceService()
    {
        var expected = new[]
        {
            new FiscalYearDto
            {
                Id = Guid.NewGuid(),
                FiscalYearCode = "FY2026",
                FiscalYearName = "Fiscal Year 2026",
                Year = 2026,
                StartDate = new DateTime(2026, 1, 1),
                EndDate = new DateTime(2026, 12, 31),
                IsClosed = false,
                IsLocked = false
            }
        };
        var fiscalPeriods = new Mock<IFiscalPeriodService>();
        fiscalPeriods
            .Setup(service => service.GetFiscalYearsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var controller = new ProcurementPlansController(
            Mock.Of<IProcurementPlanService>(),
            fiscalPeriods.Object,
            Mock.Of<ILogger<ProcurementPlansController>>());

        var action = await controller.GetPlanningFiscalYears(CancellationToken.None);

        var ok = action.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(expected);
        fiscalPeriods.Verify(
            service => service.GetFiscalYearsAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
