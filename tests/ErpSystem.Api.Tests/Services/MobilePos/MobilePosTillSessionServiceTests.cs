using ErpSystem.Api.Controllers.MobilePos;
using ErpSystem.Api.Services.MobilePos;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.MobilePos;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.MobilePos;

public sealed class MobilePosTillSessionServiceTests
{
    [Theory]
    [InlineData(nameof(MobilePosRuntimeController.GetCurrentTillSession))]
    [InlineData(nameof(MobilePosRuntimeController.OpenTillSession))]
    public void TillSessionEndpoints_ShouldRequireMobileAndFinanceTillPermissions(string actionName)
    {
        var policies = typeof(MobilePosRuntimeController).GetMethod(actionName)!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy);

        policies.Should().BeEquivalentTo(
            MobilePosPermissions.OperateTill,
            FinancePermissions.OperateCashTills);
    }

    [Fact]
    public async Task OpenAsync_ShouldUseAssignedLiquidityAccountAndStoreLocalBusinessDate()
    {
        var foundation = new Mock<IMobilePosFoundationService>();
        var cashierTills = new Mock<ICashierTillService>();
        var liquidityAccountId = Guid.NewGuid();
        foundation.Setup(service => service.GetBootstrapAsync("installation-123456", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Bootstrap(liquidityAccountId));
        cashierTills.Setup(service => service.OpenSessionAsync(
                It.IsAny<OpenCashierTillSessionDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OpenCashierTillSessionDto request, CancellationToken _) => new CashierTillSessionDto
            {
                Id = Guid.NewGuid(),
                LiquidityAccountId = request.LiquidityAccountId,
                BusinessDate = request.BusinessDate,
                OpeningFloatAmount = request.OpeningFloatAmount
            });
        var service = new MobilePosTillSessionService(foundation.Object, cashierTills.Object);

        var result = await service.OpenAsync(new MobilePosOpenTillSessionRequestDto
        {
            InstallationId = "installation-123456",
            OpeningFloatAmount = 125m,
            OpeningNotes = "Opening count"
        }, CancellationToken.None);

        result.LiquidityAccountId.Should().Be(liquidityAccountId);
        result.BusinessDate.Should().Be(new DateTime(2026, 10, 9));
        cashierTills.Verify(value => value.OpenSessionAsync(
            It.Is<OpenCashierTillSessionDto>(request =>
                request.LiquidityAccountId == liquidityAccountId
                && request.BusinessDate == new DateTime(2026, 10, 9)
                && request.OpeningFloatAmount == 125m
                && request.OpeningNotes == "Opening count"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCurrentAsync_ShouldRejectASessionForAnotherLiquidityAccount()
    {
        var foundation = new Mock<IMobilePosFoundationService>();
        var cashierTills = new Mock<ICashierTillService>();
        var currentId = Guid.NewGuid();
        var bootstrap = Bootstrap(Guid.NewGuid());
        bootstrap.CurrentTillSessionId = currentId;
        foundation.Setup(service => service.GetBootstrapAsync("installation-123456", It.IsAny<CancellationToken>()))
            .ReturnsAsync(bootstrap);
        cashierTills.Setup(service => service.GetSessionAsync(currentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CashierTillSessionDto { Id = currentId, LiquidityAccountId = Guid.NewGuid() });
        var service = new MobilePosTillSessionService(foundation.Object, cashierTills.Object);

        var action = () => service.GetCurrentAsync("installation-123456", CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*assigned Mobile POS till*");
    }

    private static MobilePosBootstrapDto Bootstrap(Guid liquidityAccountId) => new()
    {
        ServerTimeUtc = new DateTime(2026, 10, 9, 23, 30, 0, DateTimeKind.Utc),
        Store = new MobilePosStoreDto { TimeZoneId = "Africa/Accra" },
        Till = new MobilePosTillDto { LiquidityAccountId = liquidityAccountId }
    };
}
