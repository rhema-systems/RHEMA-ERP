using ErpSystem.Api.Controllers.MobilePos;
using ErpSystem.Api.Services.MobilePos;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.MobilePos;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.MobilePos;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using FinancePaymentMethod = ErpSystem.Core.Entities.Finance.PaymentMethod;

namespace ErpSystem.Api.Tests.Services.MobilePos;

public sealed class MobilePosTillSessionServiceTests
{
    [Theory]
    [InlineData(nameof(MobilePosRuntimeController.GetCurrentTillSession))]
    [InlineData(nameof(MobilePosRuntimeController.OpenTillSession))]
    [InlineData(nameof(MobilePosRuntimeController.GetTillReconciliation))]
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
        var service = CreateService(foundation.Object, cashierTills.Object);

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
        var service = CreateService(foundation.Object, cashierTills.Object);

        var action = () => service.GetCurrentAsync("installation-123456", CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*assigned Mobile POS till*");
    }

    [Fact]
    public async Task GetReconciliationAsync_ShouldGroupOnlyCompletedCanonicalTenders()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var tillId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var liquidityAccountId = Guid.NewGuid();
        var cash = new FinancePaymentMethod
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "CASH", Name = "Cash",
            Type = PaymentMethodType.Cash, IsActive = true
        };
        var card = new FinancePaymentMethod
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "CARD", Name = "Card",
            Type = PaymentMethodType.Card, IsActive = true
        };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"mobile-pos-till-reconciliation-{Guid.NewGuid():N}")
            .Options;
        await using var db = new ApplicationDbContext(options);
        var online = Sale(tenantId, storeId, tillId, sessionId, 80m, false, MobilePosSaleStatus.Completed);
        online.Tenders.Add(Tender(tenantId, cash, 50m, false, MobilePosTenderStatus.Completed, true));
        online.Tenders.Add(Tender(tenantId, card, 30m, false, MobilePosTenderStatus.Completed, true));
        var offline = Sale(tenantId, storeId, tillId, sessionId, 20m, true, MobilePosSaleStatus.Completed);
        offline.Tenders.Add(Tender(tenantId, cash, 20m, true, MobilePosTenderStatus.Completed, true));
        var rejected = Sale(tenantId, storeId, tillId, sessionId, 999m, false, MobilePosSaleStatus.Rejected);
        db.MobilePosSales.AddRange(online, offline, rejected);
        await db.SaveChangesAsync();

        var foundation = new Mock<IMobilePosFoundationService>();
        foundation.Setup(value => value.GetBootstrapAsync("installation-123456", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MobilePosBootstrapDto
            {
                Store = new MobilePosStoreDto { Id = storeId },
                Till = new MobilePosTillDto { Id = tillId, LiquidityAccountId = liquidityAccountId }
            });
        var cashierTills = new Mock<ICashierTillService>();
        cashierTills.Setup(value => value.GetSessionAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CashierTillSessionDto
            {
                Id = sessionId,
                LiquidityAccountId = liquidityAccountId,
                CashierUserId = userId,
                Currency = "GHS"
            });
        var currentUser = CurrentUser(tenantId, userId);
        var service = new MobilePosTillSessionService(foundation.Object, cashierTills.Object, db, currentUser.Object);

        var result = await service.GetReconciliationAsync(sessionId, "installation-123456", CancellationToken.None);

        result.CompletedSaleCount.Should().Be(2);
        result.OfflineSaleCount.Should().Be(1);
        result.RejectedSaleCount.Should().Be(1);
        result.SalesTotal.Should().Be(100m);
        result.TenderTotal.Should().Be(100m);
        result.SalesAndTendersBalance.Should().BeTrue();
        result.Tenders.Should().ContainEquivalentOf(new MobilePosTillTenderReconciliationDto
        {
            PaymentMethodId = cash.Id,
            PaymentMethodCode = "CASH",
            PaymentMethodName = "Cash",
            PaymentMethodType = PaymentMethodType.Cash.ToString(),
            TenderCount = 2,
            Amount = 70m,
            OfflineTenderCount = 1,
            OfflineAmount = 20m,
            CanonicalPaymentCount = 2
        });
    }

    private static MobilePosBootstrapDto Bootstrap(Guid liquidityAccountId) => new()
    {
        ServerTimeUtc = new DateTime(2026, 10, 9, 23, 30, 0, DateTimeKind.Utc),
        Store = new MobilePosStoreDto { TimeZoneId = "Africa/Accra" },
        Till = new MobilePosTillDto { LiquidityAccountId = liquidityAccountId }
    };

    private static MobilePosTillSessionService CreateService(
        IMobilePosFoundationService foundation,
        ICashierTillService cashierTills)
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"mobile-pos-till-session-{Guid.NewGuid():N}")
            .Options);
        return new MobilePosTillSessionService(foundation, cashierTills, db, CurrentUser(tenantId, userId).Object);
    }

    private static Mock<ICurrentUserService> CurrentUser(Guid tenantId, Guid userId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(value => value.TenantId).Returns(tenantId);
        currentUser.SetupGet(value => value.UserId).Returns(userId.ToString());
        return currentUser;
    }

    private static MobilePosSale Sale(
        Guid tenantId,
        Guid storeId,
        Guid tillId,
        Guid sessionId,
        decimal total,
        bool offline,
        MobilePosSaleStatus status) => new()
        {
            Id = Guid.NewGuid(), TenantId = tenantId, MobilePosStoreId = storeId,
            MobilePosTillId = tillId, CashierTillSessionId = sessionId, MobilePosDeviceId = Guid.NewGuid(),
            OperatorUserId = Guid.NewGuid(), ClientMutationId = Guid.NewGuid().ToString("N"),
            LocalReference = Guid.NewGuid().ToString("N"), BusinessPartnerId = Guid.NewGuid(),
            BusinessPartnerRoleId = Guid.NewGuid(), BusinessDate = new DateTime(2026, 10, 9),
            OccurredAtUtc = DateTime.UtcNow, CurrencyCode = "GHS", SubTotal = total,
            TotalAmount = total, Status = status, MobilePosOfflineGrantId = offline ? Guid.NewGuid() : null
        };

    private static MobilePosTender Tender(
        Guid tenantId,
        FinancePaymentMethod paymentMethod,
        decimal amount,
        bool offline,
        MobilePosTenderStatus status,
        bool canonicalPayment) => new()
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Sequence = 1,
            PaymentMethodId = paymentMethod.Id, PaymentMethod = paymentMethod,
            Amount = amount, WasRecordedOffline = offline, Status = status,
            CustomerPaymentId = canonicalPayment ? Guid.NewGuid() : null
        };
}
