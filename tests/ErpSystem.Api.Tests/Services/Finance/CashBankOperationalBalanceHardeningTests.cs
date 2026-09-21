using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Finance.Cash;
using ErpSystem.Api.Services.Finance.GL;
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
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class CashBankOperationalBalanceHardeningTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-CashBankBalance")]
    [Trait("Category", "CashBank")]
    public async Task CapturingReceipt_ShouldNotChangeStoredBankBalanceSnapshot()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedBankSetup(db, tenantId, "BANK-001", 1000m);
        var offset = SeedAccount(db, tenantId, "4100", AccountType.Revenue);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId, documentNumber: "RCT-202607-0001");

        await service.CreateReceiptAsync(new CreateCashReceiptDto
        {
            BankAccountId = fixture.BankAccount.Id,
            GLAccountId = offset.Id,
            TransactionDate = new DateTime(2026, 7, 5),
            Amount = 125m,
            Currency = "GHS",
            PayerName = "Customer"
        });

        var bank = await db.BankAccounts.SingleAsync(b => b.Id == fixture.BankAccount.Id);
        bank.CurrentBalance.Should().Be(1000m);
        bank.AvailableBalance.Should().Be(1000m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CashBankBalance")]
    [Trait("Category", "CashBank")]
    public async Task CapturingPayment_ShouldNotChangeStoredBankBalanceSnapshot()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedBankSetup(db, tenantId, "BANK-001", 1000m);
        var offset = SeedAccount(db, tenantId, "6100", AccountType.Expense);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId, documentNumber: "CPY-202607-0001");

        await service.CreatePaymentAsync(new CreateCashPaymentDto
        {
            BankAccountId = fixture.BankAccount.Id,
            GLAccountId = offset.Id,
            TransactionDate = new DateTime(2026, 7, 5),
            Amount = 125m,
            Currency = "GHS",
            PayeeName = "Supplier"
        });

        var bank = await db.BankAccounts.SingleAsync(b => b.Id == fixture.BankAccount.Id);
        bank.CurrentBalance.Should().Be(1000m);
        bank.AvailableBalance.Should().Be(1000m);
    }

    [Theory]
    [InlineData(CashTransactionType.Receipt, 1100)]
    [InlineData(CashTransactionType.Payment, 900)]
    [Trait("Batch", "FinanceGoLive-CashBankBalance")]
    [Trait("Category", "CashBank")]
    public async Task ApprovedReceiptOrPaymentPosting_ShouldUpdateStoredSnapshotOnce(CashTransactionType transactionType, decimal expectedBalance)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedCashTransactionAsync(db, tenantId, transactionType, 1000m, CashTransactionApprovalStatus.Approved);
        var service = CreateService(db, tenantId);

        await service.PostAsync(fixture.Transaction.Id);
        await service.PostAsync(fixture.Transaction.Id);

        var bank = await db.BankAccounts.SingleAsync(b => b.Id == fixture.BankAccount.Id);
        bank.CurrentBalance.Should().Be(expectedBalance);
        bank.AvailableBalance.Should().Be(expectedBalance);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CashBankBalance")]
    [Trait("Category", "CashBank")]
    public async Task CancelledUnpostedTransaction_ShouldNotAffectStoredBankBalanceSnapshot()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedCashTransactionAsync(db, tenantId, CashTransactionType.Payment, 1000m, CashTransactionApprovalStatus.Captured);
        var service = CreateService(db, tenantId);

        await service.CancelAsync(fixture.Transaction.Id, "Entered in error");

        var bank = await db.BankAccounts.SingleAsync(b => b.Id == fixture.BankAccount.Id);
        bank.CurrentBalance.Should().Be(1000m);
        bank.AvailableBalance.Should().Be(1000m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CashBankBalance")]
    [Trait("Category", "CashBank")]
    public async Task TransferCapture_ShouldNotChangeBalancesBeforePosting()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var from = SeedBankSetup(db, tenantId, "BANK-001", 500m);
        var to = SeedBankSetup(db, tenantId, "BANK-002", 50m, "1010");
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId, documentNumber: "TRF-202607-0001");

        await service.CreateTransferAsync(new CreateBankTransferDto
        {
            FromBankAccountId = from.BankAccount.Id,
            ToBankAccountId = to.BankAccount.Id,
            TransactionDate = new DateTime(2026, 7, 5),
            Amount = 100m,
            Description = "Captured transfer"
        });

        (await db.BankAccounts.SingleAsync(b => b.Id == from.BankAccount.Id)).CurrentBalance.Should().Be(500m);
        (await db.BankAccounts.SingleAsync(b => b.Id == to.BankAccount.Id)).CurrentBalance.Should().Be(50m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CashBankBalance")]
    [Trait("Category", "CashBank")]
    public async Task ApprovedTransferPosting_ShouldUpdateSourceAndDestinationSnapshotsOnce()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedBankTransferAsync(db, tenantId, 500m, 50m, CashTransactionApprovalStatus.Approved);
        var service = CreateService(db, tenantId);

        await service.PostAsync(fixture.FromTransaction.Id);
        await service.PostAsync(fixture.FromTransaction.Id);

        var fromBank = await db.BankAccounts.SingleAsync(b => b.Id == fixture.FromBankAccount.Id);
        var toBank = await db.BankAccounts.SingleAsync(b => b.Id == fixture.ToBankAccount.Id);
        fromBank.CurrentBalance.Should().Be(400m);
        fromBank.AvailableBalance.Should().Be(400m);
        toBank.CurrentBalance.Should().Be(150m);
        toBank.AvailableBalance.Should().Be(150m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CashBankBalance")]
    [Trait("Category", "CashBank")]
    public async Task CrossTenantBankAccount_ShouldNotAffectOtherTenantBalanceSnapshot()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedCashTransactionAsync(db, tenantId, CashTransactionType.Receipt, 1000m, CashTransactionApprovalStatus.Approved);
        var other = SeedBankSetup(db, otherTenantId, "BANK-OTH", 750m);
        await db.SaveChangesAsync();
        fixture.Transaction.BankAccountId = other.BankAccount.Id;
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Transaction.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cash/bank transaction bank account belongs to another tenant.");
        (await db.BankAccounts.SingleAsync(b => b.Id == other.BankAccount.Id)).CurrentBalance.Should().Be(750m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CashBankBalance")]
    [Trait("Category", "CashBank")]
    public async Task BalanceDiagnosticQuery_ShouldDetectStoredSnapshotMismatchAgainstPostedGl()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedCashTransactionAsync(db, tenantId, CashTransactionType.Receipt, 0m, CashTransactionApprovalStatus.Approved);
        var service = CreateService(db, tenantId);
        await service.PostAsync(fixture.Transaction.Id);
        fixture.BankAccount.CurrentBalance += 5m;
        fixture.BankAccount.AvailableBalance += 5m;
        await db.SaveChangesAsync();

        var mismatches = await DetectStoredSnapshotMismatchesAsync(db, tenantId);

        mismatches.Should().ContainSingle(m => m.BankAccountId == fixture.BankAccount.Id);
        mismatches.Single().StoredCurrentBalance.Should().Be(105m);
        mismatches.Single().ExpectedCurrentBalance.Should().Be(100m);
    }

    private static async Task<IReadOnlyList<BalanceMismatch>> DetectStoredSnapshotMismatchesAsync(ApplicationDbContext db, Guid tenantId)
    {
        var bankAccounts = await db.BankAccounts
            .Where(b => b.TenantId == tenantId && !b.IsDeleted)
            .ToListAsync();
        var result = new List<BalanceMismatch>();

        foreach (var bank in bankAccounts)
        {
            if (!bank.GLAccountId.HasValue)
            {
                continue;
            }

            var postedMovement = await db.AccountTransactions
                .Where(t =>
                    t.TenantId == tenantId &&
                    t.AccountId == bank.GLAccountId.Value &&
                    t.PostingStatus == "Posted" &&
                    !t.IsDeleted)
                .SumAsync(t => t.DebitAmount - t.CreditAmount);
            // Governed openings are included in postedMovement. The deprecated bank-master
            // OpeningBalance snapshot must never be added to ledger authority.
            var expected = postedMovement;
            if (expected != bank.CurrentBalance || expected != bank.AvailableBalance)
            {
                result.Add(new BalanceMismatch(bank.Id, bank.CurrentBalance, bank.AvailableBalance, expected));
            }
        }

        return result;
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"cash-bank-balance-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static CashTransactionService CreateService(
        ApplicationDbContext db,
        Guid tenantId,
        string documentNumber = "CBT-202607-0001")
    {
        var currentUser = CreateCurrentUserService(tenantId);
        var auditService = new FinanceAuditService(
            db,
            currentUser.Object,
            new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-cash-bank-balance" }
            });
        var postingEngine = new FinancePostingEngine(
            db,
            currentUser.Object,
            Mock.Of<ILogger<FinancePostingEngine>>(),
            auditService);
        var tenantSettings = new Mock<ITenantSettingsService>();
        tenantSettings.Setup(x => x.GetBaseCurrencyAsync()).ReturnsAsync("GHS");
        var documentNumbering = new Mock<IDocumentNumberingService>();
        documentNumbering
            .Setup(x => x.GenerateAsync(
                DocumentNumberingModules.Finance,
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(documentNumber);
        var accessScope = CreateUnrestrictedFinanceAccessScope();

        return new CashTransactionService(
            db,
            Mock.Of<IBankAccountService>(),
            tenantSettings.Object,
            documentNumbering.Object,
            currentUser.Object,
            accessScope.Object,
            new FinanceReversalPolicyService(db, currentUser.Object),
            postingEngine,
            auditService);
    }

    private static Mock<IFinanceAccessScopeService> CreateUnrestrictedFinanceAccessScope()
    {
        var scope = new Mock<IFinanceAccessScopeService>();
        scope.Setup(x => x.GetPermittedBankAccountIdsAsync(
                It.IsAny<FinanceAccessLevel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid>?)null);
        return scope;
    }

    private static Mock<ICurrentUserService> CreateCurrentUserService(Guid tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(x => x.UserName).Returns("cash.bank.balance");
        currentUser.SetupGet(x => x.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(x => x.UserAgent).Returns("cash-bank-balance-tests");
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.Roles).Returns(Array.Empty<string>());
        return currentUser;
    }

    private static async Task<CashBankFixture> SeedCashTransactionAsync(
        ApplicationDbContext db,
        Guid tenantId,
        CashTransactionType transactionType,
        decimal openingBalance,
        CashTransactionApprovalStatus approvalStatus)
    {
        var setup = SeedBankSetup(db, tenantId, "BANK-001", openingBalance);
        var offset = SeedAccount(db, tenantId, transactionType == CashTransactionType.Receipt ? "4100" : "6100",
            transactionType == CashTransactionType.Receipt ? AccountType.Revenue : AccountType.Expense);
        var transaction = new CashTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TransactionNumber = transactionType == CashTransactionType.Receipt ? "RCT-202607-8001" : "CPY-202607-8001",
            TransactionDate = new DateTime(2026, 7, 5),
            TransactionType = transactionType,
            BankAccountId = setup.BankAccount.Id,
            Amount = 100m,
            Currency = "GHS",
            ExchangeRate = 1m,
            BaseAmount = 100m,
            GLAccountId = offset.Id,
            Description = "Balance hardening transaction",
            IsPosted = false,
            ApprovalStatus = approvalStatus,
            IsReconciled = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        db.Set<CashTransaction>().Add(transaction);
        await db.SaveChangesAsync();
        return new CashBankFixture(transaction, setup.BankAccount, setup.BankGlAccount, offset);
    }

    private static async Task<CashBankTransferFixture> SeedBankTransferAsync(
        ApplicationDbContext db,
        Guid tenantId,
        decimal fromOpeningBalance,
        decimal toOpeningBalance,
        CashTransactionApprovalStatus approvalStatus)
    {
        var from = SeedBankSetup(db, tenantId, "BANK-001", fromOpeningBalance);
        var to = SeedBankSetup(db, tenantId, "BANK-002", toOpeningBalance, "1010");
        var fromTransaction = new CashTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TransactionNumber = "TRF-202607-8001-OUT",
            TransactionDate = new DateTime(2026, 7, 5),
            TransactionType = CashTransactionType.Transfer,
            BankAccountId = from.BankAccount.Id,
            ToBankAccountId = to.BankAccount.Id,
            Amount = 100m,
            Currency = "GHS",
            ExchangeRate = 1m,
            BaseAmount = 100m,
            Description = "Balance hardening transfer",
            ApprovalStatus = approvalStatus,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        var toTransaction = new CashTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TransactionNumber = "TRF-202607-8001-IN",
            TransactionDate = new DateTime(2026, 7, 5),
            TransactionType = CashTransactionType.Transfer,
            BankAccountId = to.BankAccount.Id,
            ToBankAccountId = from.BankAccount.Id,
            Amount = 100m,
            Currency = "GHS",
            ExchangeRate = 1m,
            BaseAmount = 100m,
            Description = "Balance hardening transfer",
            ApprovalStatus = approvalStatus,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        db.Set<CashTransaction>().AddRange(fromTransaction, toTransaction);
        await db.SaveChangesAsync();
        return new CashBankTransferFixture(fromTransaction, toTransaction, from.BankAccount, to.BankAccount);
    }

    private static BankSetup SeedBankSetup(
        ApplicationDbContext db,
        Guid tenantId,
        string accountNumber,
        decimal openingBalance,
        string glAccountNumber = "1000")
    {
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var bankGl = SeedAccount(db, tenantId, glAccountNumber, AccountType.Asset);
        var bank = new BankAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountNumber = accountNumber,
            AccountName = $"Bank {accountNumber}",
            BankName = "Test Bank",
            Currency = "GHS",
            AccountType = BankAccountType.Checking,
            GLAccountId = bankGl.Id,
            OpeningBalance = openingBalance,
            CurrentBalance = openingBalance,
            AvailableBalance = openingBalance,
            IsActive = true,
            OpeningDate = new DateTime(2026, 7, 1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        db.BankAccounts.Add(bank);
        return new BankSetup(bank, bankGl);
    }

    private static void SeedTenant(ApplicationDbContext db, Guid tenantId)
    {
        if (db.Tenants.Local.Any(t => t.Id == tenantId) || db.Tenants.Any(t => t.Id == tenantId))
        {
            return;
        }

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = $"Tenant {tenantId:N}"[..20],
            Code = tenantId.ToString("N")[..6].ToUpperInvariant(),
            Status = TenantStatus.Active,
            BaseCurrency = "GHS"
        });
        db.FinanceSettings.Add(new FinanceSettings
        {
            Id = Guid.NewGuid(), TenantId = tenantId, BaseCurrency = "GHS",
            ReferenceNumber = "FIN-CASH-BALANCE", Status = "Active"
        });
        db.AccountingBooks.Add(new AccountingBook
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "IFRS", Name = "IFRS Primary",
            Purpose = "Primary", BookType = AccountingBookType.PrimaryFull,
            LifecycleStatus = AccountingBookLifecycleStatus.Active, FunctionalCurrencyCode = "GHS",
            IsDefault = true, IsActive = true, AllowsPosting = true
        });
    }

    private static void SeedOpenPeriod(ApplicationDbContext db, Guid tenantId)
    {
        if (db.FiscalPeriods.Local.Any(p => p.TenantId == tenantId && p.PeriodCode == "2026-07") ||
            db.FiscalPeriods.Any(p => p.TenantId == tenantId && p.PeriodCode == "2026-07"))
        {
            return;
        }

        var fiscalYear = new FiscalYear
        {
            Id = Guid.NewGuid(), TenantId = tenantId, FiscalYearName = "Fiscal Year 2026",
            FiscalYearCode = "FY2026", Year = 2026, FiscalYearType = "Calendar",
            StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31),
            Status = "Open", IsActive = true
        };
        var period = new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearId = fiscalYear.Id,
            PeriodName = "July 2026",
            PeriodCode = "2026-07",
            PeriodNumber = 7,
            PeriodType = PeriodType.Monthly,
            StartDate = new DateTime(2026, 7, 1),
            EndDate = new DateTime(2026, 7, 31),
            PeriodDays = 31,
            PeriodStatus = "Open",
            IsOpen = true,
            IsClosed = false,
            IsLocked = false
        };
        db.FiscalYears.Add(fiscalYear);
        db.FiscalPeriods.Add(period);
        FinancePostingAuthorityFixture.SeedExactBookPeriod(db, tenantId, period);
    }

    private static Account SeedAccount(
        ApplicationDbContext db,
        Guid tenantId,
        string accountNumber,
        AccountType accountType)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountCode = accountNumber,
            AccountNumber = accountNumber,
            AccountName = $"Account {accountNumber}",
            AccountType = accountType,
            Status = AccountStatus.Active,
            CurrencyCode = "GHS",
            AllowDirectPosting = true
        };

        db.Accounts.Add(account);
        var book = db.AccountingBooks.Local.Single(item => item.TenantId == tenantId && item.Code == "IFRS");
        FinancePostingAuthorityFixture.SeedEnabledBookMappings(db, tenantId, book, account);
        return account;
    }

    private sealed record BankSetup(BankAccount BankAccount, Account BankGlAccount);

    private sealed record CashBankFixture(
        CashTransaction Transaction,
        BankAccount BankAccount,
        Account BankGlAccount,
        Account OffsetAccount);

    private sealed record CashBankTransferFixture(
        CashTransaction FromTransaction,
        CashTransaction ToTransaction,
        BankAccount FromBankAccount,
        BankAccount ToBankAccount);

    private sealed record BalanceMismatch(
        Guid BankAccountId,
        decimal StoredCurrentBalance,
        decimal StoredAvailableBalance,
        decimal ExpectedCurrentBalance);
}
