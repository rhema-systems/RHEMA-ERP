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

    [Theory]
    [InlineData(nameof(MobilePosRuntimeController.GetTillCloseSubmission))]
    [InlineData(nameof(MobilePosRuntimeController.SubmitTillClose))]
    public void TillCloseEndpoints_ShouldRequireMobileCloseAndFinanceTillPermissions(string actionName)
    {
        var policies = typeof(MobilePosRuntimeController).GetMethod(actionName)!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy);

        policies.Should().BeEquivalentTo(
            MobilePosPermissions.CloseTill,
            FinancePermissions.OperateCashTills);
    }

    [Fact]
    public void TillReviewQueue_ShouldRequireMobileAndFinanceReviewPermissions()
    {
        var policies = typeof(MobilePosAdministrationController)
            .GetMethod(nameof(MobilePosAdministrationController.GetTillCloseSubmissions))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy);

        policies.Should().BeEquivalentTo(
            MobilePosPermissions.ReviewTill,
            FinancePermissions.ReviewCashTillClosures);
    }

    [Fact]
    public void TillCloseReport_ShouldRequireMobileAndFinanceReviewPermissions()
    {
        var policies = typeof(MobilePosAdministrationController)
            .GetMethod(nameof(MobilePosAdministrationController.GetTillCloseReport))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy);

        policies.Should().BeEquivalentTo(
            MobilePosPermissions.ReviewTill,
            FinancePermissions.ReviewCashTillClosures);
    }

    [Fact]
    public void BankDepositProposal_ShouldRequireMobileReviewAndFinanceDepositPermissions()
    {
        var policies = typeof(MobilePosAdministrationController)
            .GetMethod(nameof(MobilePosAdministrationController.CreateBankDepositProposal))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy);

        policies.Should().BeEquivalentTo(
            MobilePosPermissions.ReviewTill,
            FinancePermissions.CreateBankDeposits);
    }

    [Fact]
    public void PendingSyncResolution_ShouldRequireResolveAndReviewPermissions()
    {
        var policies = typeof(MobilePosAdministrationController)
            .GetMethod(nameof(MobilePosAdministrationController.ResolvePendingSync))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy);

        policies.Should().BeEquivalentTo(
            MobilePosPermissions.ResolveSync,
            MobilePosPermissions.ReviewTill,
            FinancePermissions.ReviewCashTillClosures);
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

    [Fact]
    public async Task SubmitCloseAsync_ShouldRejectPendingMutationsWhenPolicyDisallowsThem()
    {
        var fixture = CreateCloseFixture(allowPendingSync: false);
        var service = new MobilePosTillSessionService(
            fixture.Foundation.Object,
            fixture.CashierTills.Object,
            fixture.Db,
            fixture.CurrentUser.Object);

        var action = () => service.SubmitCloseAsync(
            fixture.SessionId,
            CloseRequest("pending-mutation-001"),
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not allow a pending-sync submission*");
        fixture.CashierTills.Verify(value => value.SubmitCountAsync(
            It.IsAny<Guid>(), It.IsAny<SubmitCashierTillCountDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SubmitCloseAsync_ShouldPersistDeterministicPendingSyncEvidence()
    {
        var fixture = CreateCloseFixture(allowPendingSync: true);
        var service = new MobilePosTillSessionService(
            fixture.Foundation.Object,
            fixture.CashierTills.Object,
            fixture.Db,
            fixture.CurrentUser.Object);

        var result = await service.SubmitCloseAsync(
            fixture.SessionId,
            CloseRequest("pending-mutation-002", "pending-mutation-001", "pending-mutation-002"),
            CancellationToken.None);

        result.Status.Should().Be(MobilePosTillCloseSubmissionStatus.PendingSync);
        result.PendingClientMutationIds.Should().Equal("pending-mutation-001", "pending-mutation-002");
        result.PendingMutationCount.Should().Be(2);
        result.PendingMutationDigest.Should().Be(
            MobilePosTillSessionService.HashPendingMutationIds(result.PendingClientMutationIds.ToArray()));
        (await fixture.Db.MobilePosTillCloseSubmissions.SingleAsync())
            .PolicyAllowedPendingSync.Should().BeTrue();
        fixture.CashierTills.Verify(value => value.SubmitCountAsync(
            fixture.SessionId,
            It.Is<SubmitCashierTillCountDto>(request =>
                request.CountLines.Count == 1
                && request.CountLines[0].Denomination == 20m
                && request.CountLines[0].Quantity == 3),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitCloseAsync_ShouldBeReadyForReviewWhenNothingIsPending()
    {
        var fixture = CreateCloseFixture(allowPendingSync: false);
        var service = new MobilePosTillSessionService(
            fixture.Foundation.Object,
            fixture.CashierTills.Object,
            fixture.Db,
            fixture.CurrentUser.Object);

        var result = await service.SubmitCloseAsync(
            fixture.SessionId,
            CloseRequest(),
            CancellationToken.None);

        result.Status.Should().Be(MobilePosTillCloseSubmissionStatus.ReadyForReview);
        result.PendingMutationCount.Should().Be(0);
    }

    [Fact]
    public async Task ResolvePendingSyncAsync_ShouldEnforceMakerChecker()
    {
        var fixture = CreateCloseFixture(allowPendingSync: true);
        fixture.Db.MobilePosTillCloseSubmissions.Add(CloseSubmission(
            fixture, MobilePosTillCloseSubmissionStatus.PendingSync, "pending-mutation-001"));
        await fixture.Db.SaveChangesAsync();
        var service = new MobilePosTillSessionService(
            fixture.Foundation.Object,
            fixture.CashierTills.Object,
            fixture.Db,
            fixture.CurrentUser.Object);
        var submission = await fixture.Db.MobilePosTillCloseSubmissions.SingleAsync();

        var action = () => service.ResolvePendingSyncAsync(
            submission.Id,
            new MobilePosResolvePendingSyncRequestDto
            {
                Reason = "Cashier is attempting to approve their own exception.",
                RowVersion = Convert.ToBase64String(submission.RowVersion)
            },
            CancellationToken.None);

        await action.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*cannot resolve their own*");
    }

    [Fact]
    public async Task FinalizationGuard_ShouldBlockUnresolvedPendingMutations()
    {
        var fixture = CreateCloseFixture(allowPendingSync: true);
        fixture.Db.MobilePosTills.Add(new MobilePosTill
        {
            Id = fixture.TillId,
            TenantId = fixture.TenantId,
            MobilePosStoreId = fixture.StoreId,
            TillNumber = "TILL-01",
            Name = "Main till",
            Status = MobilePosTillStatus.Active,
            LiquidityAccountId = fixture.LiquidityAccountId
        });
        fixture.Db.MobilePosTillCloseSubmissions.Add(CloseSubmission(
            fixture, MobilePosTillCloseSubmissionStatus.PendingSync, "pending-mutation-001"));
        await fixture.Db.SaveChangesAsync();
        var guard = new MobilePosTillFinalizationGuard(fixture.Db, fixture.CurrentUser.Object);

        var action = () => guard.PrepareFinalizationAsync(
            fixture.SessionId, fixture.LiquidityAccountId, CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*1 unresolved Mobile POS mutation*");
    }

    [Fact]
    public async Task FinalizationGuard_ShouldFinalizeWhenPendingMutationsHaveSynchronized()
    {
        var fixture = CreateCloseFixture(allowPendingSync: true, reviewerUser: true);
        fixture.Db.MobilePosTills.Add(new MobilePosTill
        {
            Id = fixture.TillId,
            TenantId = fixture.TenantId,
            MobilePosStoreId = fixture.StoreId,
            TillNumber = "TILL-01",
            Name = "Main till",
            Status = MobilePosTillStatus.Active,
            LiquidityAccountId = fixture.LiquidityAccountId
        });
        fixture.Db.MobilePosTillCloseSubmissions.Add(CloseSubmission(
            fixture, MobilePosTillCloseSubmissionStatus.PendingSync, "pending-mutation-001"));
        fixture.Db.MobileMutationReceipts.Add(new MobileMutationReceipt
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            MobilePosDeviceId = fixture.DeviceId,
            ClientMutationId = "pending-mutation-001",
            CommandType = "CompleteSale",
            SchemaVersion = 1,
            RequestHash = new string('A', 64),
            Status = MobileMutationReceiptStatus.Completed,
            StartedAtUtc = DateTime.UtcNow,
            CompletedAtUtc = DateTime.UtcNow,
            LastAttemptAtUtc = DateTime.UtcNow
        });
        await fixture.Db.SaveChangesAsync();
        var guard = new MobilePosTillFinalizationGuard(fixture.Db, fixture.CurrentUser.Object);

        await guard.PrepareFinalizationAsync(
            fixture.SessionId, fixture.LiquidityAccountId, CancellationToken.None);
        await fixture.Db.SaveChangesAsync();

        var submission = await fixture.Db.MobilePosTillCloseSubmissions.SingleAsync();
        submission.Status.Should().Be(MobilePosTillCloseSubmissionStatus.Finalized);
        submission.FinalizedByUserId.Should().Be(fixture.UserId);
        submission.FinalizedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task FinalizationGuard_ShouldMarkReturnedSubmissionForRecount()
    {
        var fixture = CreateCloseFixture(allowPendingSync: false, reviewerUser: true);
        fixture.Db.MobilePosTills.Add(new MobilePosTill
        {
            Id = fixture.TillId,
            TenantId = fixture.TenantId,
            MobilePosStoreId = fixture.StoreId,
            TillNumber = "TILL-01",
            Name = "Main till",
            Status = MobilePosTillStatus.Active,
            LiquidityAccountId = fixture.LiquidityAccountId
        });
        fixture.Db.MobilePosTillCloseSubmissions.Add(CloseSubmission(
            fixture, MobilePosTillCloseSubmissionStatus.ReadyForReview));
        await fixture.Db.SaveChangesAsync();
        var guard = new MobilePosTillFinalizationGuard(fixture.Db, fixture.CurrentUser.Object);

        await guard.PrepareReturnForRecountAsync(
            fixture.SessionId, fixture.LiquidityAccountId, CancellationToken.None);
        await fixture.Db.SaveChangesAsync();

        var submission = await fixture.Db.MobilePosTillCloseSubmissions.SingleAsync();
        submission.Status.Should().Be(MobilePosTillCloseSubmissionStatus.ReturnedForRecount);
        submission.UpdatedBy.Should().Be("HQ Reviewer");
    }

    [Fact]
    public async Task CreateBankDepositProposalAsync_ShouldUseFinalizedSessionEntriesAndLinkFinanceDeposit()
    {
        var fixture = CreateCloseFixture(allowPendingSync: false, reviewerUser: true);
        var bankAccountId = Guid.NewGuid();
        var depositId = Guid.NewGuid();
        var openedAt = DateTime.UtcNow.AddHours(-8);
        var cutoff = DateTime.UtcNow.AddMinutes(5);
        fixture.Db.CashierTillSessions.Add(new CashierTillSession
        {
            Id = fixture.SessionId,
            TenantId = fixture.TenantId,
            SessionNumber = "CTS-001",
            LiquidityAccountId = fixture.LiquidityAccountId,
            BusinessDate = DateTime.UtcNow.Date,
            Currency = "GHS",
            CashierUserId = fixture.CashierUserId,
            CashierName = "Cashier",
            Status = CashierTillSessionStatus.Closed,
            OpenedAt = openedAt,
            OpenedById = fixture.CashierUserId,
            ActivityCutoffAt = cutoff
        });
        var submission = CloseSubmission(fixture, MobilePosTillCloseSubmissionStatus.Finalized);
        fixture.Db.MobilePosTillCloseSubmissions.Add(submission);
        var entry = new LiquidityAccountEntry
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            LiquidityAccountId = fixture.LiquidityAccountId,
            EntryNumber = "LE-001",
            EntryDate = DateTime.UtcNow.Date,
            EntryType = LiquidityEntryType.CustomerReceipt,
            Direction = LiquidityEntryDirection.Increase,
            Amount = 250m,
            AllocatedAmount = 50m,
            Currency = "GHS",
            SourceDocumentType = "CustomerPayment",
            SourceDocumentId = Guid.NewGuid(),
            CreatedAt = openedAt.AddHours(1)
        };
        fixture.Db.LiquidityAccountEntries.Add(entry);
        await fixture.Db.SaveChangesAsync();
        var banking = new Mock<IBankingSettlementService>();
        banking.Setup(value => value.CreateDepositAsync(
                It.IsAny<CreateBankDepositDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BankDepositDto { Id = depositId, DepositNumber = "BD-001" });
        var service = new MobilePosTillSessionService(
            fixture.Foundation.Object,
            fixture.CashierTills.Object,
            fixture.Db,
            fixture.CurrentUser.Object,
            banking.Object);

        var result = await service.CreateBankDepositProposalAsync(
            submission.Id,
            new MobilePosCreateDepositProposalRequestDto
            {
                BankAccountId = bankAccountId,
                DepositDate = DateTime.UtcNow.Date,
                DepositReference = "SLIP-001",
                RowVersion = Convert.ToBase64String(submission.RowVersion)
            },
            CancellationToken.None);

        result.Id.Should().Be(depositId);
        banking.Verify(value => value.CreateDepositAsync(
            It.Is<CreateBankDepositDto>(request =>
                request.BankAccountId == bankAccountId
                && request.DepositReference == "SLIP-001"
                && request.Notes!.Contains($"[MobilePosTillClose:{submission.Id:N}]", StringComparison.Ordinal)
                && request.Allocations.Count == 1
                && request.Allocations[0].LiquidityAccountEntryId == entry.Id
                && request.Allocations[0].AllocationType == BankDepositAllocationType.Receipt
                && request.Allocations[0].Amount == 200m),
            It.IsAny<CancellationToken>()), Times.Once);
        var persisted = await fixture.Db.MobilePosTillCloseSubmissions.SingleAsync();
        persisted.BankDepositBatchId.Should().Be(depositId);
        persisted.BankDepositProposedByUserId.Should().Be(fixture.UserId);
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

    private static CloseFixture CreateCloseFixture(bool allowPendingSync, bool reviewerUser = false)
    {
        var fixture = new CloseFixture
        {
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            CashierUserId = Guid.NewGuid(),
            StoreId = Guid.NewGuid(),
            TillId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            LiquidityAccountId = Guid.NewGuid()
        };
        if (!reviewerUser)
            fixture.CashierUserId = fixture.UserId;
        fixture.Db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"mobile-pos-close-{Guid.NewGuid():N}")
            .Options);
        fixture.CurrentUser = CurrentUser(fixture.TenantId, fixture.UserId);
        fixture.CurrentUser.SetupGet(value => value.UserName).Returns(reviewerUser ? "HQ Reviewer" : "Cashier");
        var bootstrap = new MobilePosBootstrapDto
        {
            Store = new MobilePosStoreDto
            {
                Id = fixture.StoreId,
                OfflinePolicyId = allowPendingSync ? Guid.NewGuid() : null
            },
            Till = new MobilePosTillDto
            {
                Id = fixture.TillId,
                LiquidityAccountId = fixture.LiquidityAccountId
            },
            Device = new MobilePosDeviceDto { Id = fixture.DeviceId },
            OfflinePolicy = new MobilePosOfflinePolicyDto
            {
                Id = Guid.NewGuid(),
                AllowDayEndSubmissionWithPendingSync = allowPendingSync
            }
        };
        fixture.Foundation.Setup(value => value.GetBootstrapAsync(
                "installation-123456", It.IsAny<CancellationToken>()))
            .ReturnsAsync(bootstrap);
        fixture.CashierTills.Setup(value => value.GetSessionAsync(
                fixture.SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => fixture.Session());
        fixture.CashierTills.Setup(value => value.SubmitCountAsync(
                fixture.SessionId, It.IsAny<SubmitCashierTillCountDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => fixture.Session());
        return fixture;
    }

    private static MobilePosSubmitTillCloseRequestDto CloseRequest(params string[] pendingIds) => new()
    {
        InstallationId = "installation-123456",
        SessionRowVersion = Convert.ToBase64String([1]),
        CountLines =
        [
            new CashierTillCountLineInputDto { Denomination = 20m, Quantity = 3 }
        ],
        PendingClientMutationIds = pendingIds
    };

    private static MobilePosTillCloseSubmission CloseSubmission(
        CloseFixture fixture,
        MobilePosTillCloseSubmissionStatus status,
        params string[] pendingIds) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = fixture.TenantId,
        CashierTillSessionId = fixture.SessionId,
        MobilePosStoreId = fixture.StoreId,
        MobilePosTillId = fixture.TillId,
        MobilePosDeviceId = fixture.DeviceId,
        SubmittedByUserId = fixture.CashierUserId,
        SubmittedAtUtc = DateTime.UtcNow,
        PolicyAllowedPendingSync = true,
        PendingMutationCount = pendingIds.Length,
        PendingMutationIdsJson = System.Text.Json.JsonSerializer.Serialize(pendingIds),
        PendingMutationDigest = MobilePosTillSessionService.HashPendingMutationIds(pendingIds),
        Status = status,
        RowVersion = [1]
    };

    private sealed class CloseFixture
    {
        public Guid TenantId { get; set; }
        public Guid UserId { get; set; }
        public Guid CashierUserId { get; set; }
        public Guid StoreId { get; set; }
        public Guid TillId { get; set; }
        public Guid DeviceId { get; set; }
        public Guid SessionId { get; set; }
        public Guid LiquidityAccountId { get; set; }
        public ApplicationDbContext Db { get; set; } = null!;
        public Mock<IMobilePosFoundationService> Foundation { get; } = new();
        public Mock<ICashierTillService> CashierTills { get; } = new();
        public Mock<ICurrentUserService> CurrentUser { get; set; } = null!;

        public CashierTillSessionDto Session() => new()
        {
            Id = SessionId,
            LiquidityAccountId = LiquidityAccountId,
            CashierUserId = CashierUserId,
            Currency = "GHS",
            RowVersion = Convert.ToBase64String([1])
        };
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
