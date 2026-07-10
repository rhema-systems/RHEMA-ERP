using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
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

public sealed class FinancePostingEngineTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-4")]
    [Trait("Category", "PostingEngine")]
    public async Task PostAsync_ShouldCreatePostedLedgerEntriesAndPostingEvent_WhenBalancedSameTenant()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var cashAccount = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var revenueAccount = SeedAccount(db, tenantId, "4000", AccountType.Revenue);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var request = CreateRequest(tenantId, cashAccount.Id, revenueAccount.Id);

        var result = await service.PostAsync(request);

        result.PostingStatus.Should().Be("Posted");
        result.WasDuplicate.Should().BeFalse();
        result.TotalDebitAmount.Should().Be(100m);
        result.TotalCreditAmount.Should().Be(100m);
        result.FunctionalCurrencyCode.Should().Be("GHS");
        result.JournalEntryId.Should().NotBeEmpty();
        result.PostingEventId.Should().NotBeEmpty();

        var journal = await db.JournalEntries
            .Include(j => j.Transactions)
            .SingleAsync(j => j.Id == result.JournalEntryId);
        journal.TenantId.Should().Be(tenantId);
        journal.PostingStatus.Should().Be("Posted");
        journal.TotalDebitAmount.Should().Be(100m);
        journal.TotalCreditAmount.Should().Be(100m);
        journal.Transactions.Should().HaveCount(2);
        journal.Transactions.Should().OnlyContain(t => t.TenantId == tenantId && t.PostingStatus == "Posted");

        var postingEvent = await db.FinancePostingEvents.SingleAsync(e => e.Id == result.PostingEventId);
        postingEvent.TenantId.Should().Be(tenantId);
        postingEvent.JournalEntryId.Should().Be(result.JournalEntryId);

        cashAccount.Balance.Should().Be(100m);
        revenueAccount.Balance.Should().Be(100m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-4")]
    [Trait("Category", "PostingEngine")]
    public async Task PostAsync_ShouldApplyAccountBalanceMovementUsingNormalBalanceDirection()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var assetAccount = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var expenseAccount = SeedAccount(db, tenantId, "5000", AccountType.Expense);
        var liabilityAccount = SeedAccount(db, tenantId, "2000", AccountType.Liability);
        var equityAccount = SeedAccount(db, tenantId, "3000", AccountType.Equity);
        var revenueAccount = SeedAccount(db, tenantId, "4000", AccountType.Revenue);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);

        await service.PostAsync(new FinancePostingRequestDto
        {
            SourceModule = "TEST",
            SourceDocumentType = "NormalBalanceDocument",
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentTenantId = tenantId,
            PostingAction = "Post",
            SourceDocumentReference = "NB-001",
            Description = "Normal balance direction test",
            PostingDate = new DateTime(2026, 7, 4),
            JournalType = "System Generated",
            BookClassification = "IFRS",
            FunctionalCurrencyCode = "GHS",
            Lines = new[]
            {
                new FinancePostingLineDto { AccountId = assetAccount.Id, Description = "Asset debit", DebitAmount = 100m },
                new FinancePostingLineDto { AccountId = expenseAccount.Id, Description = "Expense debit", DebitAmount = 40m },
                new FinancePostingLineDto { AccountId = revenueAccount.Id, Description = "Revenue debit", DebitAmount = 15m },
                new FinancePostingLineDto { AccountId = liabilityAccount.Id, Description = "Liability credit", CreditAmount = 100m },
                new FinancePostingLineDto { AccountId = equityAccount.Id, Description = "Equity credit", CreditAmount = 25m },
                new FinancePostingLineDto { AccountId = assetAccount.Id, Description = "Asset credit", CreditAmount = 30m }
            }
        });

        assetAccount.Balance.Should().Be(70m);
        expenseAccount.Balance.Should().Be(40m);
        liabilityAccount.Balance.Should().Be(100m);
        equityAccount.Balance.Should().Be(25m);
        revenueAccount.Balance.Should().Be(-15m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-4")]
    [Trait("Category", "PostingEngine")]
    public async Task PostAsync_ShouldRejectUnbalancedPosting()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var debitAccount = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var creditAccount = SeedAccount(db, tenantId, "4000", AccountType.Revenue);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var request = CreateRequest(tenantId, debitAccount.Id, creditAccount.Id);
        request.Lines = new[]
        {
            request.Lines[0],
            new FinancePostingLineDto
            {
                AccountId = creditAccount.Id,
                Description = "Revenue",
                CreditAmount = 90m
            }
        };

        var act = () => service.PostAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Posting is not balanced. Total debits must equal total credits.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-4")]
    [Trait("Category", "PostingEngine")]
    public async Task PostAsync_ShouldReturnExistingPosting_WhenSourceDocumentPostedAgain()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var debitAccount = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var creditAccount = SeedAccount(db, tenantId, "4000", AccountType.Revenue);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var request = CreateRequest(tenantId, debitAccount.Id, creditAccount.Id);

        var first = await service.PostAsync(request);
        var second = await service.PostAsync(request);

        second.WasDuplicate.Should().BeTrue();
        second.PostingEventId.Should().Be(first.PostingEventId);
        second.JournalEntryId.Should().Be(first.JournalEntryId);
        (await db.JournalEntries.CountAsync()).Should().Be(1);
        (await db.FinancePostingEvents.CountAsync()).Should().Be(1);
        debitAccount.Balance.Should().Be(100m);
        creditAccount.Balance.Should().Be(100m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-4")]
    [Trait("Category", "PostingEngine")]
    public async Task PostAsync_ShouldRejectCrossTenantSourceDocument()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var debitAccount = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var creditAccount = SeedAccount(db, tenantId, "4000", AccountType.Revenue);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var request = CreateRequest(tenantId, debitAccount.Id, creditAccount.Id);
        request.SourceDocumentTenantId = otherTenantId;

        var act = () => service.PostAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Source document belongs to another tenant.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-4")]
    [Trait("Category", "PostingEngine")]
    public async Task PostAsync_ShouldRejectClosedPeriod()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId, isOpen: false, isClosed: true);
        var debitAccount = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var creditAccount = SeedAccount(db, tenantId, "4000", AccountType.Revenue);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var request = CreateRequest(tenantId, debitAccount.Id, creditAccount.Id);

        var act = () => service.PostAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Posting period is not open.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-PeriodClose")]
    [Trait("Category", "PostingEngine")]
    public async Task PostAsync_ShouldAuditPostingBlockedByClosedPeriod_WhenFinanceAuditIsConfigured()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId, isOpen: false, isClosed: true);
        var debitAccount = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var creditAccount = SeedAccount(db, tenantId, "4000", AccountType.Revenue);
        await db.SaveChangesAsync();

        var currentUser = CreateCurrentUser(tenantId);
        var auditService = new FinanceAuditService(db, currentUser.Object, new HttpContextAccessor());
        var service = new FinancePostingEngine(
            db,
            currentUser.Object,
            Mock.Of<ILogger<FinancePostingEngine>>(),
            auditService);
        var request = CreateRequest(tenantId, debitAccount.Id, creditAccount.Id);

        var act = () => service.PostAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Posting period is not open.");
        (await db.AuditLogs.CountAsync(a =>
            a.TenantId == tenantId &&
            a.Action == FinanceAuditEvents.PostingBlockedPeriodClosedLocked)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-4")]
    [Trait("Category", "PostingEngine")]
    public async Task PostAsync_ShouldRejectInactiveAccount()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var debitAccount = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var inactiveCreditAccount = SeedAccount(db, tenantId, "4000", AccountType.Revenue, AccountStatus.Inactive);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var request = CreateRequest(tenantId, debitAccount.Id, inactiveCreditAccount.Id);

        var act = () => service.PostAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot post to inactive GL account(s): 4000.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-4")]
    [Trait("Category", "PostingEngine")]
    public async Task PostAsync_ShouldRejectMissingTenantContext()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var debitAccount = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var creditAccount = SeedAccount(db, tenantId, "4000", AccountType.Revenue);
        await db.SaveChangesAsync();

        var service = CreateService(
            db,
            tenantId,
            new Dictionary<string, string> { ["sub"] = Guid.NewGuid().ToString() });
        var request = CreateRequest(tenantId, debitAccount.Id, creditAccount.Id);

        var act = () => service.PostAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Finance tenant context is required.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-4")]
    [Trait("Category", "PostingEngine")]
    public async Task PostAsync_ShouldRejectOtherTenantAccount()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedTenant(db, otherTenantId, "OTHER");
        SeedOpenPeriod(db, tenantId);
        var debitAccount = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var otherTenantCreditAccount = SeedAccount(db, otherTenantId, "4000", AccountType.Revenue);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var request = CreateRequest(tenantId, debitAccount.Id, otherTenantCreditAccount.Id);

        var act = () => service.PostAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("One or more posting accounts were not found for this tenant.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-4")]
    [Trait("Category", "PostingEngine")]
    public async Task GetReversalPlanAsync_ShouldReturnOppositeLinesWithoutPostingReversal()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var debitAccount = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var creditAccount = SeedAccount(db, tenantId, "4000", AccountType.Revenue);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var posted = await service.PostAsync(CreateRequest(tenantId, debitAccount.Id, creditAccount.Id));

        var plan = await service.GetReversalPlanAsync(posted.PostingEventId, "Correction required", new DateTime(2026, 7, 5));

        plan.IsDefined.Should().BeTrue();
        plan.OriginalPostingEventId.Should().Be(posted.PostingEventId);
        plan.OriginalJournalEntryId.Should().Be(posted.JournalEntryId);
        plan.PostingAction.Should().Be("Reverse");
        plan.ReversalDate.Should().Be(new DateTime(2026, 7, 5));
        plan.ReversalLines.Should().HaveCount(2);
        plan.ReversalLines.Sum(l => l.DebitAmount).Should().Be(100m);
        plan.ReversalLines.Sum(l => l.CreditAmount).Should().Be(100m);
        (await db.JournalEntries.CountAsync()).Should().Be(1);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"finance-posting-engine-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static FinancePostingEngine CreateService(
        ApplicationDbContext db,
        Guid tenantId,
        IDictionary<string, string>? claims = null)
    {
        var currentUser = CreateCurrentUser(tenantId, claims);
        return new FinancePostingEngine(db, currentUser.Object, Mock.Of<ILogger<FinancePostingEngine>>());
    }

    private static Mock<ICurrentUserService> CreateCurrentUser(
        Guid tenantId,
        IDictionary<string, string>? claims = null)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.Claims).Returns(claims ?? new Dictionary<string, string>());
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(x => x.UserName).Returns("finance.poster");
        currentUser.SetupGet(x => x.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(x => x.UserAgent).Returns("finance-posting-engine-tests");
        return currentUser;
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
        bool isClosed = false,
        bool isLocked = false)
    {
        var period = new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearId = Guid.NewGuid(),
            PeriodName = "FY2026",
            PeriodCode = "2026",
            PeriodNumber = 1,
            PeriodType = PeriodType.Monthly,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31),
            PeriodDays = 365,
            PeriodStatus = isClosed ? "Closed" : isOpen ? "Open" : "Future",
            IsOpen = isOpen,
            IsClosed = isClosed,
            IsLocked = isLocked
        };

        db.FiscalPeriods.Add(period);
        return period;
    }

    private static Account SeedAccount(
        ApplicationDbContext db,
        Guid tenantId,
        string accountNumber,
        AccountType accountType,
        AccountStatus status = AccountStatus.Active)
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
            AllowDirectPosting = true
        };

        db.Accounts.Add(account);
        return account;
    }

    private static FinancePostingRequestDto CreateRequest(Guid tenantId, Guid debitAccountId, Guid creditAccountId)
    {
        return new FinancePostingRequestDto
        {
            SourceModule = "TEST",
            SourceDocumentType = "TestDocument",
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentTenantId = tenantId,
            PostingAction = "Post",
            SourceDocumentReference = "SRC-001",
            Description = "Batch 4 posting engine test",
            PostingDate = new DateTime(2026, 7, 4),
            JournalType = "System Generated",
            BookClassification = "IFRS",
            FunctionalCurrencyCode = "GHS",
            Lines = new[]
            {
                new FinancePostingLineDto
                {
                    AccountId = debitAccountId,
                    Description = "Cash",
                    DebitAmount = 100m
                },
                new FinancePostingLineDto
                {
                    AccountId = creditAccountId,
                    Description = "Revenue",
                    CreditAmount = 100m
                }
            }
        };
    }
}
