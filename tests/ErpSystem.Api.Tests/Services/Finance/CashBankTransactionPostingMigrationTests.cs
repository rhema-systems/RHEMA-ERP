using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Finance.Cash;
using ErpSystem.Api.Services.Finance.GL;
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

public sealed class CashBankTransactionPostingMigrationTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-CashBankPosting")]
    [Trait("Category", "CashBank")]
    public async Task ReceiptTransaction_ShouldPostThroughFinancePostingEngineAndCreateAuditEvent()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedCashTransactionAsync(db, tenantId, CashTransactionType.Receipt);
        var service = CreateService(db, tenantId);

        var result = await service.PostAsync(fixture.Transaction.Id);

        result.JournalEntryId.Should().NotBeNull();
        result.IsPosted.Should().BeTrue();

        var postingEvent = await db.FinancePostingEvents.SingleAsync(e =>
            e.TenantId == tenantId &&
            e.SourceModule == "CASHBANK" &&
            e.SourceDocumentType == "CashBankReceipt" &&
            e.SourceDocumentId == fixture.Transaction.Id &&
            e.PostingAction == "Post");
        postingEvent.JournalEntryId.Should().Be(result.JournalEntryId);

        var journal = await db.JournalEntries
            .Include(j => j.Transactions)
            .SingleAsync(j => j.Id == result.JournalEntryId);
        journal.PostingStatus.Should().Be("Posted");
        journal.SourceDocumentType.Should().Be("CashBankReceipt");
        journal.Transactions.Single(t => t.AccountId == fixture.BankGlAccount.Id).DebitAmount.Should().Be(100m);
        journal.Transactions.Single(t => t.AccountId == fixture.OffsetAccount.Id).CreditAmount.Should().Be(100m);

        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.CashBankTransactionPostedAfterApproval && a.TenantId == tenantId)).Should().Be(1);
        // The posting engine keeps Account.Balance as a read-side snapshot for legacy balance APIs.
        fixture.BankGlAccount.Balance.Should().Be(100m);
        fixture.OffsetAccount.Balance.Should().Be(100m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CashBankPosting")]
    [Trait("Category", "CashBank")]
    public async Task PaymentTransaction_ShouldPostCorrectDebitCreditDirection()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedCashTransactionAsync(db, tenantId, CashTransactionType.Payment);
        var service = CreateService(db, tenantId);

        var result = await service.PostAsync(fixture.Transaction.Id);

        var journal = await db.JournalEntries
            .Include(j => j.Transactions)
            .SingleAsync(j => j.Id == result.JournalEntryId);
        journal.SourceDocumentType.Should().Be("CashBankPayment");
        journal.Transactions.Single(t => t.AccountId == fixture.OffsetAccount.Id).DebitAmount.Should().Be(100m);
        journal.Transactions.Single(t => t.AccountId == fixture.BankGlAccount.Id).CreditAmount.Should().Be(100m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CashBankPosting")]
    [Trait("Category", "CashBank")]
    public async Task BankTransfer_ShouldPostCorrectDirectionAndLinkBothTransferLegs()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedBankTransferAsync(db, tenantId);
        var service = CreateService(db, tenantId);

        var result = await service.PostAsync(fixture.FromTransaction.Id);

        result.JournalEntryId.Should().NotBeNull();

        var from = await db.Set<CashTransaction>().SingleAsync(t => t.Id == fixture.FromTransaction.Id);
        var to = await db.Set<CashTransaction>().SingleAsync(t => t.Id == fixture.ToTransaction.Id);
        from.JournalEntryId.Should().Be(result.JournalEntryId);
        to.JournalEntryId.Should().Be(result.JournalEntryId);
        from.IsPosted.Should().BeTrue();
        to.IsPosted.Should().BeTrue();

        var journal = await db.JournalEntries
            .Include(j => j.Transactions)
            .SingleAsync(j => j.Id == result.JournalEntryId);
        journal.SourceDocumentType.Should().Be("CashBankTransfer");
        journal.SourceDocumentId.Should().Be(fixture.FromTransaction.Id);
        journal.Transactions.Single(t => t.AccountId == fixture.ToBankGlAccount.Id).DebitAmount.Should().Be(100m);
        journal.Transactions.Single(t => t.AccountId == fixture.FromBankGlAccount.Id).CreditAmount.Should().Be(100m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CashBankPosting")]
    [Trait("Category", "CashBank")]
    public async Task CrossTenantBankAccount_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedCashTransactionAsync(db, tenantId, CashTransactionType.Receipt);
        SeedTenant(db, otherTenantId, "OTH");
        var otherBankGl = SeedAccount(db, otherTenantId, "1010", AccountType.Asset);
        var otherBank = SeedBankAccount(db, otherTenantId, "BANK-OTH", otherBankGl.Id);
        fixture.Transaction.BankAccountId = otherBank.Id;
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Transaction.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cash/bank transaction bank account belongs to another tenant.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CashBankPosting")]
    [Trait("Category", "CashBank")]
    public async Task CrossTenantOffsetAccount_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedCashTransactionAsync(db, tenantId, CashTransactionType.Receipt);
        SeedTenant(db, otherTenantId, "OTH");
        var otherOffset = SeedAccount(db, otherTenantId, "4800", AccountType.Revenue);
        fixture.Transaction.GLAccountId = otherOffset.Id;
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Transaction.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("The cash receipt offset account GL account was not found for this tenant.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CashBankPosting")]
    [Trait("Category", "CashBank")]
    public async Task NonPostableOffsetAccount_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedCashTransactionAsync(db, tenantId, CashTransactionType.Payment);
        fixture.OffsetAccount.AllowDirectPosting = false;
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Transaction.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("The cash payment offset account GL account does not allow direct posting.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CashBankPosting")]
    [Trait("Category", "CashBank")]
    public async Task ClosedPeriod_ShouldBeRejectedByPostingEngine()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedCashTransactionAsync(db, tenantId, CashTransactionType.Receipt, periodIsOpen: false, periodIsClosed: true);
        var service = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Transaction.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Posting period is not open.");
        fixture.Transaction.JournalEntryId.Should().BeNull();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CashBankPosting")]
    [Trait("Category", "CashBank")]
    public async Task DuplicateCashBankPost_ShouldReturnExistingPostingAndAuditDuplicate()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedCashTransactionAsync(db, tenantId, CashTransactionType.Receipt);
        var service = CreateService(db, tenantId);

        var first = await service.PostAsync(fixture.Transaction.Id);
        var second = await service.PostAsync(fixture.Transaction.Id);

        second.JournalEntryId.Should().Be(first.JournalEntryId);
        (await db.JournalEntries.CountAsync(j => j.SourceDocumentType == "CashBankReceipt")).Should().Be(1);
        (await db.FinancePostingEvents.CountAsync(e => e.SourceDocumentType == "CashBankReceipt")).Should().Be(1);
        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.CashBankTransactionDuplicatePostingAttempt)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CashBankPosting")]
    [Trait("Category", "CashBank")]
    public async Task PostedCashBankTransaction_ShouldNotBeDeletedByMutation()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedCashTransactionAsync(db, tenantId, CashTransactionType.Payment);
        var service = CreateService(db, tenantId);
        await service.PostAsync(fixture.Transaction.Id);

        var act = () => service.DeleteAsync(fixture.Transaction.Id);

        await act.Should().ThrowAsync<Exception>()
            .WithMessage("Cannot delete posted transaction");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CashBankPosting")]
    [Trait("Category", "CashBank")]
    public async Task PostedFlagWithoutPostingEvent_ShouldBeRejectedForDiagnostics()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedCashTransactionAsync(db, tenantId, CashTransactionType.Receipt);
        fixture.Transaction.IsPosted = true;
        fixture.Transaction.JournalEntryId = Guid.NewGuid();
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Transaction.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cash/bank transaction is marked posted or linked to a journal without a valid finance posting event. Run posting back-reference diagnostics before retrying.");
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"cash-bank-posting-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static CashTransactionService CreateService(ApplicationDbContext db, Guid tenantId)
    {
        var currentUser = CreateCurrentUserService(tenantId);
        var auditService = new FinanceAuditService(
            db,
            currentUser.Object,
            new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-cash-bank-posting" }
            });
        var postingEngine = new FinancePostingEngine(
            db,
            currentUser.Object,
            Mock.Of<ILogger<FinancePostingEngine>>(),
            auditService);
        var tenantSettings = new Mock<ITenantSettingsService>();
        tenantSettings.Setup(x => x.GetBaseCurrencyAsync()).ReturnsAsync("GHS");

        return new CashTransactionService(
            db,
            Mock.Of<IBankAccountService>(),
            tenantSettings.Object,
            Mock.Of<IDocumentNumberingService>(),
            currentUser.Object,
            postingEngine,
            auditService);
    }

    private static Mock<ICurrentUserService> CreateCurrentUserService(Guid tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(x => x.UserName).Returns("cash.bank.poster");
        currentUser.SetupGet(x => x.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(x => x.UserAgent).Returns("cash-bank-posting-tests");
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.Roles).Returns(Array.Empty<string>());
        return currentUser;
    }

    private static async Task<CashBankFixture> SeedCashTransactionAsync(
        ApplicationDbContext db,
        Guid tenantId,
        CashTransactionType transactionType,
        bool periodIsOpen = true,
        bool periodIsClosed = false)
    {
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId, periodIsOpen, periodIsClosed);
        var bankGl = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var offset = SeedAccount(db, tenantId, transactionType == CashTransactionType.Receipt ? "4100" : "6100",
            transactionType == CashTransactionType.Receipt ? AccountType.Revenue : AccountType.Expense);
        var bankAccount = SeedBankAccount(db, tenantId, "BANK-001", bankGl.Id);
        var transaction = new CashTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TransactionNumber = transactionType == CashTransactionType.Receipt ? "RCT-202607-0001" : "CPY-202607-0001",
            TransactionDate = new DateTime(2026, 7, 5),
            TransactionType = transactionType,
            BankAccountId = bankAccount.Id,
            Amount = 100m,
            Currency = "GHS",
            ExchangeRate = 1m,
            BaseAmount = 100m,
            GLAccountId = offset.Id,
            Description = "Standalone cash/bank transaction",
            IsPosted = false,
            ApprovalStatus = CashTransactionApprovalStatus.Approved,
            IsReconciled = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        db.Set<CashTransaction>().Add(transaction);
        await db.SaveChangesAsync();
        return new CashBankFixture(transaction, bankAccount, bankGl, offset);
    }

    private static async Task<CashBankTransferFixture> SeedBankTransferAsync(ApplicationDbContext db, Guid tenantId)
    {
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var fromBankGl = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var toBankGl = SeedAccount(db, tenantId, "1010", AccountType.Asset);
        var fromBank = SeedBankAccount(db, tenantId, "BANK-001", fromBankGl.Id);
        var toBank = SeedBankAccount(db, tenantId, "BANK-002", toBankGl.Id);
        var fromTransaction = new CashTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TransactionNumber = "TRF-202607-0001-OUT",
            TransactionDate = new DateTime(2026, 7, 5),
            TransactionType = CashTransactionType.Transfer,
            BankAccountId = fromBank.Id,
            ToBankAccountId = toBank.Id,
            Amount = 100m,
            Currency = "GHS",
            ExchangeRate = 1m,
            BaseAmount = 100m,
            Description = "Transfer from operating to savings",
            ApprovalStatus = CashTransactionApprovalStatus.Approved,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        var toTransaction = new CashTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TransactionNumber = "TRF-202607-0001-IN",
            TransactionDate = new DateTime(2026, 7, 5),
            TransactionType = CashTransactionType.Transfer,
            BankAccountId = toBank.Id,
            ToBankAccountId = fromBank.Id,
            Amount = 100m,
            Currency = "GHS",
            ExchangeRate = 1m,
            BaseAmount = 100m,
            Description = "Transfer from operating to savings",
            ApprovalStatus = CashTransactionApprovalStatus.Approved,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        db.Set<CashTransaction>().AddRange(fromTransaction, toTransaction);
        await db.SaveChangesAsync();
        return new CashBankTransferFixture(fromTransaction, toTransaction, fromBank, toBank, fromBankGl, toBankGl);
    }

    private static void SeedTenant(ApplicationDbContext db, Guid tenantId, string code = "TEN")
    {
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = $"Tenant {code}",
            Code = code,
            Status = TenantStatus.Active,
            BaseCurrency = "GHS"
        });
    }

    private static FiscalPeriod SeedOpenPeriod(
        ApplicationDbContext db,
        Guid tenantId,
        bool isOpen = true,
        bool isClosed = false)
    {
        var period = new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearId = Guid.NewGuid(),
            PeriodName = "July 2026",
            PeriodCode = "2026-07",
            PeriodNumber = 7,
            PeriodType = PeriodType.Monthly,
            StartDate = new DateTime(2026, 7, 1),
            EndDate = new DateTime(2026, 7, 31),
            PeriodDays = 31,
            PeriodStatus = isClosed ? "Closed" : isOpen ? "Open" : "Future",
            IsOpen = isOpen,
            IsClosed = isClosed,
            IsLocked = false
        };

        db.FiscalPeriods.Add(period);
        return period;
    }

    private static Account SeedAccount(
        ApplicationDbContext db,
        Guid tenantId,
        string accountNumber,
        AccountType accountType,
        AccountStatus status = AccountStatus.Active,
        bool allowDirectPosting = true)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountCode = accountNumber,
            AccountNumber = accountNumber,
            AccountName = $"Account {accountNumber}",
            AccountType = accountType,
            Status = status,
            CurrencyCode = "GHS",
            AllowDirectPosting = allowDirectPosting
        };

        db.Accounts.Add(account);
        return account;
    }

    private static BankAccount SeedBankAccount(ApplicationDbContext db, Guid tenantId, string accountNumber, Guid glAccountId)
    {
        var bank = new BankAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountNumber = accountNumber,
            AccountName = $"Bank {accountNumber}",
            BankName = "Test Bank",
            Currency = "GHS",
            AccountType = BankAccountType.Checking,
            GLAccountId = glAccountId,
            IsActive = true,
            OpeningDate = new DateTime(2026, 7, 1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        db.BankAccounts.Add(bank);
        return bank;
    }

    private sealed record CashBankFixture(
        CashTransaction Transaction,
        BankAccount BankAccount,
        Account BankGlAccount,
        Account OffsetAccount);

    private sealed record CashBankTransferFixture(
        CashTransaction FromTransaction,
        CashTransaction ToTransaction,
        BankAccount FromBankAccount,
        BankAccount ToBankAccount,
        Account FromBankGlAccount,
        Account ToBankGlAccount);
}
