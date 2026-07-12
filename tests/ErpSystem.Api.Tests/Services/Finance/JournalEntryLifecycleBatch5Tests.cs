using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Services;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class JournalEntryLifecycleBatch5Tests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-5")]
    [Trait("Category", "PostingEngine")]
    public void FinancePostingEvent_ShouldHaveStrictTenantSourceDocumentActionUniqueIndex()
    {
        using var db = CreateContext();

        var entityType = db.Model.FindEntityType(typeof(FinancePostingEvent));
        var hasStrictIndex = entityType?.GetIndexes().Any(index =>
            index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual(new[]
            {
                nameof(FinancePostingEvent.TenantId),
                nameof(FinancePostingEvent.SourceDocumentType),
                nameof(FinancePostingEvent.SourceDocumentId),
                nameof(FinancePostingEvent.PostingAction)
            })) == true;

        hasStrictIndex.Should().BeTrue();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-5")]
    [Trait("Category", "JournalLifecycle")]
    public async Task DraftJournal_ShouldBeEditable()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var (debitAccount, creditAccount) = await SeedTenantPeriodAndAccountsAsync(db, tenantId);
        var service = CreateJournalService(db, tenantId);
        var journal = await service.CreateJournalEntryAsync(CreateJournalDto(debitAccount.Id, creditAccount.Id));

        var updated = await service.UpdateJournalEntryAsync(journal.Id, new UpdateJournalEntryDto
        {
            Description = "Updated draft journal"
        });

        updated.Description.Should().Be("Updated draft journal");
        updated.PostingStatus.Should().Be("Draft");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-5")]
    [Trait("Category", "JournalLifecycle")]
    public async Task SubmittedJournal_ShouldNotPostWithoutApproval()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var (debitAccount, creditAccount) = await SeedTenantPeriodAndAccountsAsync(db, tenantId);
        var service = CreateJournalService(db, tenantId);
        var journal = await service.CreateJournalEntryAsync(CreateJournalDto(debitAccount.Id, creditAccount.Id));
        await service.UpdateApprovalStatusAsync(journal.Id, "Pending Approval", "Pending");

        var act = () => service.PostJournalEntryAsync(journal.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Manual journal entries must be approved before posting.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-5")]
    [Trait("Category", "JournalLifecycle")]
    public async Task ApprovedJournal_ShouldPostThroughFinancePostingEngine()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var (debitAccount, creditAccount) = await SeedTenantPeriodAndAccountsAsync(db, tenantId);
        var service = CreateJournalService(db, tenantId);
        var journal = await service.CreateJournalEntryAsync(CreateJournalDto(debitAccount.Id, creditAccount.Id));
        await service.UpdateApprovalStatusAsync(journal.Id, "Approved", "Approved", Guid.NewGuid());

        var posted = await service.PostJournalEntryAsync(journal.Id);

        posted.PostingStatus.Should().Be("Posted");
        posted.PostedDate.Should().NotBeNull();
        posted.Transactions.Should().OnlyContain(t => t.TransactionType == "Debit" || t.TransactionType == "Credit");
        (await db.FinancePostingEvents.CountAsync(e =>
            e.TenantId == tenantId &&
            e.SourceDocumentType == "ManualJournalEntry" &&
            e.SourceDocumentId == journal.Id &&
            e.PostingAction == "Post")).Should().Be(1);

        var storedDebitAccount = await db.Accounts.SingleAsync(a => a.Id == debitAccount.Id);
        var storedCreditAccount = await db.Accounts.SingleAsync(a => a.Id == creditAccount.Id);
        // Manual journals use the same posting-engine balance snapshot maintenance as subledger posts.
        storedDebitAccount.Balance.Should().Be(100m);
        storedCreditAccount.Balance.Should().Be(100m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-5")]
    [Trait("Category", "JournalLifecycle")]
    public async Task UnbalancedJournal_ShouldNotSubmitOrPost()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var (debitAccount, creditAccount) = await SeedTenantPeriodAndAccountsAsync(db, tenantId);
        var journal = SeedUnbalancedJournal(db, tenantId, debitAccount.Id, creditAccount.Id, "Draft");
        await db.SaveChangesAsync();
        var service = CreateJournalService(db, tenantId);

        var submitAct = () => service.ValidateJournalEntryReadyForSubmissionAsync(journal.Id);
        await submitAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Journal Entry must be balanced.");

        journal.PostingStatus = "Approved";
        await db.SaveChangesAsync();
        var postAct = () => service.PostJournalEntryAsync(journal.Id);
        await postAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Journal Entry must be balanced.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-5")]
    [Trait("Category", "JournalLifecycle")]
    public async Task PostedJournal_ShouldNotBeEditedOrDeleted()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var (debitAccount, creditAccount) = await SeedTenantPeriodAndAccountsAsync(db, tenantId);
        var service = CreateJournalService(db, tenantId);
        var journal = await service.CreateJournalEntryAsync(CreateJournalDto(debitAccount.Id, creditAccount.Id));
        await service.UpdateApprovalStatusAsync(journal.Id, "Approved", "Approved", Guid.NewGuid());
        await service.PostJournalEntryAsync(journal.Id);

        var updateAct = () => service.UpdateJournalEntryAsync(journal.Id, new UpdateJournalEntryDto { Description = "Illegal update" });
        var deleteAct = () => service.DeleteJournalEntryAsync(journal.Id);

        await updateAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Only Draft journal entries can be updated.");
        await deleteAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Only Draft journal entries can be deleted.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-5")]
    [Trait("Category", "JournalLifecycle")]
    public async Task DuplicateManualJournalPost_ShouldReturnExistingPostedJournalWithoutDuplicateEvent()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var (debitAccount, creditAccount) = await SeedTenantPeriodAndAccountsAsync(db, tenantId);
        var service = CreateJournalService(db, tenantId);
        var journal = await service.CreateJournalEntryAsync(CreateJournalDto(debitAccount.Id, creditAccount.Id));
        await service.UpdateApprovalStatusAsync(journal.Id, "Approved", "Approved", Guid.NewGuid());

        var first = await service.PostJournalEntryAsync(journal.Id);
        var second = await service.PostJournalEntryAsync(journal.Id);

        second.Id.Should().Be(first.Id);
        second.PostingStatus.Should().Be("Posted");
        (await db.FinancePostingEvents.CountAsync(e => e.SourceDocumentId == journal.Id && e.PostingAction == "Post")).Should().Be(1);
        (await db.JournalEntries.CountAsync()).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-5")]
    [Trait("Category", "JournalLifecycle")]
    public async Task CreateJournal_ShouldRejectCrossTenantAccountLine()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var (_, creditAccount) = await SeedTenantPeriodAndAccountsAsync(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var otherAccount = SeedAccount(db, otherTenantId, "1000", AccountType.Asset);
        await db.SaveChangesAsync();
        var service = CreateJournalService(db, tenantId);

        var act = () => service.CreateJournalEntryAsync(CreateJournalDto(otherAccount.Id, creditAccount.Id));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Account {otherAccount.Id} was not found.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-5")]
    [Trait("Category", "JournalLifecycle")]
    public async Task ApprovedJournal_ShouldNotPostIntoClosedPeriod()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var closedPeriod = SeedPeriod(db, tenantId, isOpen: false, isClosed: true);
        var debitAccount = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var creditAccount = SeedAccount(db, tenantId, "4000", AccountType.Revenue);
        var journal = SeedBalancedJournal(db, tenantId, closedPeriod.Id, debitAccount.Id, creditAccount.Id, "Approved");
        await db.SaveChangesAsync();
        var service = CreateJournalService(db, tenantId);

        var act = () => service.PostJournalEntryAsync(journal.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Fiscal period '{closedPeriod.PeriodName}' is not open for posting.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-5")]
    [Trait("Category", "JournalLifecycle")]
    public async Task PostedJournal_ShouldBeCorrectedThroughReversalJournal()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var (debitAccount, creditAccount) = await SeedTenantPeriodAndAccountsAsync(db, tenantId);
        var service = CreateJournalService(db, tenantId);
        var journal = await service.CreateJournalEntryAsync(CreateJournalDto(debitAccount.Id, creditAccount.Id));
        await service.UpdateApprovalStatusAsync(journal.Id, "Approved", "Approved", Guid.NewGuid());
        await service.PostJournalEntryAsync(journal.Id);

        var reversal = await service.ReverseJournalEntryAsync(journal.Id, "Correction required", new DateTime(2026, 7, 5));

        reversal.PostingStatus.Should().Be("Posted");
        reversal.OriginalJournalId.Should().Be(journal.Id);
        reversal.TotalDebit.Should().Be(100m);
        reversal.TotalCredit.Should().Be(100m);

        var original = await db.JournalEntries.Include(j => j.Transactions).SingleAsync(j => j.Id == journal.Id);
        original.PostingStatus.Should().Be("Reversed");
        original.IsReversed.Should().BeTrue();
        original.ReversalJournalEntryId.Should().Be(reversal.Id);
        original.Transactions.Should().OnlyContain(t => t.IsReversed && t.ReversalTransactionId.HasValue);
        (await db.FinancePostingEvents.CountAsync(e => e.SourceDocumentId == journal.Id && e.PostingAction == "Reverse")).Should().Be(1);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"journal-lifecycle-batch5-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static JournalEntryService CreateJournalService(ApplicationDbContext db, Guid tenantId)
    {
        var currentUser = CreateCurrentUser(tenantId);
        var engine = new FinancePostingEngine(db, currentUser.Object, Mock.Of<ILogger<FinancePostingEngine>>());

        var gl = new Mock<IGeneralLedgerService>();
        var sequence = 1;
        gl.Setup(x => x.GenerateJournalEntryNumberAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => $"JE-2026-{sequence++:0000}");

        var audit = new Mock<IAuditLogService>();
        audit.Setup(x => x.LogUserActionAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        var notification = new Mock<INotificationService>();
        var books = new Mock<IAccountingBookService>();

        return new JournalEntryService(
            db,
            currentUser.Object,
            gl.Object,
            audit.Object,
            notification.Object,
            books.Object,
            engine);
    }

    private static Mock<ICurrentUserService> CreateCurrentUser(Guid tenantId)
    {
        var userId = Guid.NewGuid();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(x => x.UserId).Returns(userId.ToString());
        currentUser.SetupGet(x => x.UserName).Returns("finance.lifecycle");
        currentUser.SetupGet(x => x.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(x => x.UserAgent).Returns("tests");
        return currentUser;
    }

    private static async Task<(Account DebitAccount, Account CreditAccount)> SeedTenantPeriodAndAccountsAsync(ApplicationDbContext db, Guid tenantId)
    {
        SeedTenant(db, tenantId);
        SeedPeriod(db, tenantId);
        var debitAccount = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var creditAccount = SeedAccount(db, tenantId, "4000", AccountType.Revenue);
        await db.SaveChangesAsync();
        return (debitAccount, creditAccount);
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

    private static FiscalPeriod SeedPeriod(
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
            AllowDirectPosting = allowDirectPosting,
            CurrencyCode = "GHS"
        };

        db.Accounts.Add(account);
        return account;
    }

    private static CreateJournalEntryDto CreateJournalDto(Guid debitAccountId, Guid creditAccountId)
    {
        return new CreateJournalEntryDto
        {
            TransactionDate = new DateTime(2026, 7, 5),
            JournalType = "General",
            Description = "Manual lifecycle journal",
            Reference = "MAN-001",
            BookClassification = "IFRS",
            Transactions = new List<CreateAccountTransactionDto>
            {
                new()
                {
                    AccountId = debitAccountId,
                    TransactionType = "Debit",
                    Amount = 100m,
                    Description = "Debit line"
                },
                new()
                {
                    AccountId = creditAccountId,
                    TransactionType = "Credit",
                    Amount = 100m,
                    Description = "Credit line"
                }
            }
        };
    }

    private static JournalEntry SeedBalancedJournal(
        ApplicationDbContext db,
        Guid tenantId,
        Guid fiscalPeriodId,
        Guid debitAccountId,
        Guid creditAccountId,
        string status)
    {
        var journal = new JournalEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            JournalEntryNumber = $"JE-{Guid.NewGuid():N}"[..20],
            EntryDate = new DateTime(2026, 7, 5),
            JournalType = "General",
            Description = "Seeded journal",
            TotalDebitAmount = 100m,
            TotalCreditAmount = 100m,
            IsBalanced = true,
            BookClassification = "IFRS",
            FiscalPeriodId = fiscalPeriodId,
            PostingStatus = status,
            ApprovalStatus = status == "Approved" ? "Approved" : null
        };

        journal.Transactions.Add(new AccountTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            JournalEntryId = journal.Id,
            AccountId = debitAccountId,
            TransactionDate = journal.EntryDate,
            DebitAmount = 100m,
            CreditAmount = 0m,
            FiscalPeriodId = fiscalPeriodId,
            BookClassification = "IFRS",
            LineNumber = 1
        });
        journal.Transactions.Add(new AccountTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            JournalEntryId = journal.Id,
            AccountId = creditAccountId,
            TransactionDate = journal.EntryDate,
            DebitAmount = 0m,
            CreditAmount = 100m,
            FiscalPeriodId = fiscalPeriodId,
            BookClassification = "IFRS",
            LineNumber = 2
        });

        db.JournalEntries.Add(journal);
        return journal;
    }

    private static JournalEntry SeedUnbalancedJournal(
        ApplicationDbContext db,
        Guid tenantId,
        Guid debitAccountId,
        Guid creditAccountId,
        string status)
    {
        var period = db.FiscalPeriods.Single(p => p.TenantId == tenantId);
        var journal = SeedBalancedJournal(db, tenantId, period.Id, debitAccountId, creditAccountId, status);
        journal.TotalCreditAmount = 90m;
        journal.IsBalanced = false;
        journal.Transactions.Last().CreditAmount = 90m;
        return journal;
    }
}
