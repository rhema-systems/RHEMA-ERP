using ErpSystem.Api.Services.Finance.AR;
using ErpSystem.Api.Services.Finance.Cash;
using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

/// <summary>
/// Release-gate coverage for WP5's physical-cash custody boundary and FIN-LIM-0013.
/// These tests assert that a till is a control envelope around canonical liquidity entries,
/// not a second editable cash balance.
/// </summary>
public sealed class CashierTillControlTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-CashierTill")]
    [Trait("Category", "CashBank")]
    public async Task TillCount_ShouldDeriveExpectedCashAndRequireIndependentClosure()
    {
        var tenantId = Guid.NewGuid();
        var cashierId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        await using var db = CreateContext();
        var till = await SeedTillAsync(db, tenantId);
        var cashier = CreateService(db, tenantId, cashierId);
        var opened = await cashier.OpenSessionAsync(new OpenCashierTillSessionDto
        {
            LiquidityAccountId = till.Id,
            BusinessDate = DateTime.UtcNow.Date,
            OpeningFloatAmount = 200m,
            OpeningNotes = "Opening float counted by cashier."
        });
        // API requests use a fresh scoped DbContext. Clear the opening request's tracked instance
        // so the submission request reloads the store-generated concurrency value.
        db.ChangeTracker.Clear();

        // The service must derive +500 receipt and -50 payment from immutable entries. No expected
        // amount is sent by the browser, preventing a cashier from editing the system total.
        db.LiquidityAccountEntries.AddRange(
            CreateEntry(tenantId, till.Id, LiquidityEntryDirection.Increase, 500m, "CustomerPayment"),
            CreateEntry(tenantId, till.Id, LiquidityEntryDirection.Decrease, 50m, "AccountTransaction"));
        await db.SaveChangesAsync();

        var submitted = await cashier.SubmitCountAsync(opened.Id, new SubmitCashierTillCountDto
        {
            RowVersion = opened.RowVersion,
            VarianceReason = "GHS 10 shortage pending supervisor review.",
            CountLines =
            [
                new CashierTillCountLineInputDto { Denomination = 200m, Quantity = 3 },
                new CashierTillCountLineInputDto { Denomination = 20m, Quantity = 2 }
            ]
        });

        submitted.Status.Should().Be(CashierTillSessionStatus.PendingReview);
        submitted.TransactionMovementAmount.Should().Be(450m);
        submitted.ExpectedClosingAmount.Should().Be(650m);
        submitted.CountedClosingAmount.Should().Be(640m);
        submitted.VarianceAmount.Should().Be(-10m);
        submitted.CustodyEntryCount.Should().Be(2);

        var selfApproval = () => cashier.ApproveClosureAsync(opened.Id, new ReviewCashierTillSessionDto
        {
            RowVersion = submitted.RowVersion,
            Comments = "Self approval is prohibited."
        });
        await selfApproval.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*cannot approve their own till closure*");

        var reviewer = CreateService(db, tenantId, reviewerId);
        var closed = await reviewer.ApproveClosureAsync(opened.Id, new ReviewCashierTillSessionDto
        {
            RowVersion = submitted.RowVersion,
            Comments = "Count and shortage explanation independently reviewed."
        });
        closed.Status.Should().Be(CashierTillSessionStatus.Closed);
        closed.ReviewedById.Should().Be(reviewerId);
        closed.ClosedAt.Should().NotBeNull();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CashierTill")]
    [Trait("Category", "CashBank")]
    public async Task TillControl_ShouldPreventOverlappingCustodyAndUnexplainedVariance()
    {
        var tenantId = Guid.NewGuid();
        var cashierId = Guid.NewGuid();
        await using var db = CreateContext();
        var till = await SeedTillAsync(db, tenantId);
        var service = CreateService(db, tenantId, cashierId);
        var opened = await service.OpenSessionAsync(new OpenCashierTillSessionDto
        {
            LiquidityAccountId = till.Id,
            BusinessDate = DateTime.UtcNow.Date,
            OpeningFloatAmount = 100m
        });

        var overlapping = () => service.OpenSessionAsync(new OpenCashierTillSessionDto
        {
            LiquidityAccountId = till.Id,
            BusinessDate = DateTime.UtcNow.Date,
            OpeningFloatAmount = 100m
        });
        await overlapping.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already has an open or pending-review custody session*");

        var unexplained = () => service.SubmitCountAsync(opened.Id, new SubmitCashierTillCountDto
        {
            RowVersion = opened.RowVersion,
            CountLines = [new CashierTillCountLineInputDto { Denomination = 20m, Quantity = 4 }]
        });
        await unexplained.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*reason is required for every non-zero till variance*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-FIN-LIM-0013")]
    [Trait("Category", "AccountsReceivable")]
    public async Task LegacyCustomerPaymentCreditNote_ShouldRejectNewWrites()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var currentUser = CreateCurrentUser(tenantId, userId);
        var service = new PaymentService(
            Mock.Of<IUnitOfWork>(),
            currentUser.Object,
            Mock.Of<ITenantSettingsService>(),
            Mock.Of<ILogger<PaymentService>>(),
            Mock.Of<IDocumentNumberingService>(),
            Mock.Of<IFinanceAccessScopeService>(),
            Mock.Of<IFinanceReversalPolicyService>());

        var action = () => service.CreateAsync(new PaymentCreateDto
        {
            CustomerId = Guid.NewGuid(),
            PaymentDate = DateTime.UtcNow,
            TotalAmount = 100m,
            PaymentMethod = "CreditNote",
            IsCreditNote = true
        });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*legacy AR payment credit-note path is retired*");
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"cashier-till-control-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new CashierTillTestDbContext(options);
    }

    private static async Task<LiquidityAccount> SeedTillAsync(ApplicationDbContext db, Guid tenantId)
    {
        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "TDC Till Control Test",
            Code = tenantId.ToString("N")[..6].ToUpperInvariant(),
            Status = TenantStatus.Active,
            BaseCurrency = "GHS"
        };
        var gl = new Account
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountCode = "1020",
            AccountNumber = "1020",
            AccountName = "Cash on Hand",
            AccountType = AccountType.Asset,
            Status = AccountStatus.Active,
            CurrencyCode = "GHS",
            AllowDirectPosting = false
        };
        var till = new LiquidityAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "TILL-TEST",
            Name = "Test Cashier Till",
            AccountType = LiquidityAccountType.CashTill,
            Currency = "GHS",
            GLAccountId = gl.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddMinutes(-1),
            CreatedBy = "seed"
        };
        db.AddRange(tenant, gl, till, new FinanceSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrency = "GHS",
            CashTillVarianceApprovalThreshold = 100m,
            RequireIndependentCashTillClosure = true,
            CreatedAt = DateTime.UtcNow.AddMinutes(-1),
            CreatedBy = "seed"
        });
        await db.SaveChangesAsync();
        return till;
    }

    private static CashierTillService CreateService(
        ApplicationDbContext db,
        Guid tenantId,
        Guid userId)
    {
        var currentUser = CreateCurrentUser(tenantId, userId);
        var numbering = new Mock<IDocumentNumberingService>();
        numbering.Setup(service => service.GenerateAsync(
                DocumentNumberingModules.Finance,
                FinanceDocumentTypes.CashTillSession,
                It.IsAny<Guid?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => $"TILL-{Guid.NewGuid():N}"[..24]);
        return new CashierTillService(db, currentUser.Object, numbering.Object);
    }

    private static Mock<ICurrentUserService> CreateCurrentUser(Guid tenantId, Guid userId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.TenantId).Returns(tenantId);
        currentUser.SetupGet(service => service.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(service => service.UserId).Returns(userId.ToString());
        currentUser.SetupGet(service => service.UserName).Returns($"user-{userId:N}");
        currentUser.SetupGet(service => service.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(service => service.UserAgent).Returns("cashier-till-control-tests");
        currentUser.SetupGet(service => service.IsAuthenticated).Returns(true);
        currentUser.SetupGet(service => service.Roles).Returns(Array.Empty<string>());
        return currentUser;
    }

    private static LiquidityAccountEntry CreateEntry(
        Guid tenantId,
        Guid tillId,
        LiquidityEntryDirection direction,
        decimal amount,
        string sourceType)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LiquidityAccountId = tillId,
            EntryNumber = $"LQE-{Guid.NewGuid():N}"[..24],
            EntryDate = DateTime.UtcNow,
            EntryType = direction == LiquidityEntryDirection.Increase
                ? LiquidityEntryType.CustomerReceipt
                : LiquidityEntryType.CashExpense,
            Direction = direction,
            Amount = amount,
            Currency = "GHS",
            SourceDocumentType = sourceType,
            SourceDocumentId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        };

    /// <summary>
    /// SQL Server supplies rowversion values, whereas EF InMemory has no compatible generator and
    /// otherwise treats every update as a missing-row concurrency failure. Only this test model
    /// disables that provider-incompatible token; production ApplicationDbContext remains unchanged.
    /// </summary>
    private sealed class CashierTillTestDbContext : ApplicationDbContext
    {
        public CashierTillTestDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<CashierTillSession>()
                .Property(item => item.RowVersion)
                .IsConcurrencyToken(false)
                .ValueGeneratedNever();
        }

    }

}
