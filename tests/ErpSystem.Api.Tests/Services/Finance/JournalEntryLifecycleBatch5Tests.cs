using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Workflow;
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

public sealed partial class JournalEntryLifecycleBatch5Tests
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
                nameof(FinancePostingEvent.AccountingBookId),
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
    public async Task ForeignDraftJournal_ShouldRetainApprovedExchangeRateEvidence()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var (debitAccount, creditAccount) = await SeedTenantPeriodAndAccountsAsync(db, tenantId);
        var rate = new ExchangeRate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrencyCode = "GHS",
            TargetCurrencyCode = "USD",
            Rate = 12.5m,
            InverseRate = 0.08m,
            EffectiveDate = new DateTime(2026, 7, 1),
            RateType = ExchangeRateType.Daily,
            QuoteSide = ExchangeRateQuoteSide.Mid,
            RateSource = "Approved test rate",
            IsActive = true,
            ApprovalStatus = RateApprovalStatus.Approved
        };
        db.ExchangeRates.Add(rate);
        await db.SaveChangesAsync();
        var request = CreateJournalDto(debitAccount.Id, creditAccount.Id);
        request.Transactions[0].CurrencyCode = "USD";
        request.Transactions[0].ForeignAmount = 8m;
        request.Transactions[0].ExchangeRate = rate.Rate;
        request.Transactions[0].ExchangeRateId = rate.Id;

        var journal = await CreateJournalService(db, tenantId).CreateJournalEntryAsync(request);

        var foreignLine = journal.Transactions.Single(line => line.TransactionType == "Debit");
        foreignLine.ExchangeRateId.Should().Be(rate.Id);
        foreignLine.ExchangeRate.Should().Be(rate.Rate);
        (await db.AccountTransactions.SingleAsync(line => line.Id == foreignLine.Id))
            .ExchangeRateId.Should().Be(rate.Id);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-5")]
    [Trait("Category", "JournalLifecycle")]
    public async Task ForeignDraftJournal_ShouldRejectDecimalWithoutRateRecord()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var (debitAccount, creditAccount) = await SeedTenantPeriodAndAccountsAsync(db, tenantId);
        var request = CreateJournalDto(debitAccount.Id, creditAccount.Id);
        request.Transactions[0].CurrencyCode = "USD";
        request.Transactions[0].ForeignAmount = 8m;
        request.Transactions[0].ExchangeRate = 12.5m;

        var act = () => CreateJournalService(db, tenantId).CreateJournalEntryAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*requires an approved USD exchange-rate record*");
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
        var (debitAccount, creditAccount) = await SeedTenantPeriodAndAccountsAsync(db, tenantId);
        var closedPeriod = await db.FiscalPeriods.SingleAsync(period => period.TenantId == tenantId);
        closedPeriod.IsOpen = false;
        closedPeriod.IsClosed = true;
        closedPeriod.PeriodStatus = "Closed";
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
        original.PostingStatus.Should().Be("Posted");
        original.IsReversed.Should().BeTrue();
        original.ReversalJournalEntryId.Should().Be(reversal.Id);
        original.Transactions.Should().OnlyContain(t => t.IsReversed && t.ReversalTransactionId.HasValue);
        original.Transactions.Should().OnlyContain(t => t.PostingStatus == "Posted");
        (await db.FinancePostingEvents.CountAsync(e => e.SourceDocumentId == journal.Id && e.PostingAction == "Reverse")).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-5")]
    [Trait("Category", "JournalInquiry")]
    public async Task GetJournalEntriesAsync_ShouldFilterProcurementSource_AndExposeSourceLineage()
    {
        var tenantId = Guid.NewGuid();
        var fiscalPeriodId = Guid.NewGuid();
        var procurementDocumentId = Guid.NewGuid();
        var postingDate = new DateTime(2026, 8, 29);
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var book = new AccountingBook
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "IFRS", Name = "IFRS Primary",
            IsDefault = true, IsActive = true, AllowsPosting = true
        };
        db.AccountingBooks.Add(book);

        db.JournalEntries.AddRange(
            new JournalEntry
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                JournalEntryNumber = "PRO-20260829-0001",
                JournalType = "System Generated",
                EntryDate = postingDate,
                Description = "Supplier onboarding token payment TEST-001",
                SourceModule = "Procurement",
                OriginModuleCode = "PROC",
                SourceDocumentId = procurementDocumentId,
                SourceDocumentType = "SupplierOnboardingTokenPayment",
                AccountingBookId = book.Id,
                BookClassification = book.Code,
                FiscalPeriodId = fiscalPeriodId,
                PostingStatus = "Posted",
                CreatedAt = postingDate
            },
            new JournalEntry
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                JournalEntryNumber = "AP-20260829-0001",
                JournalType = "System Generated",
                EntryDate = postingDate,
                Description = "Vendor invoice",
                SourceModule = "AP",
                AccountingBookId = book.Id,
                BookClassification = book.Code,
                FiscalPeriodId = fiscalPeriodId,
                PostingStatus = "Posted",
                CreatedAt = postingDate
            });
        await db.SaveChangesAsync();

        var service = CreateJournalService(db, tenantId);
        var entries = await service.GetJournalEntriesAsync(
            status: "posted",
            startDate: postingDate,
            endDate: postingDate,
            fiscalPeriodId: fiscalPeriodId,
            sourceModule: "PROCUREMENT");

        var entry = entries.Should().ContainSingle().Subject;
        entry.SourceModule.Should().Be("Procurement");
        entry.OriginModuleCode.Should().Be("PROC");
        entry.SourceDocumentType.Should().Be("SupplierOnboardingTokenPayment");
        entry.SourceDocumentId.Should().Be(procurementDocumentId);
        entry.FiscalPeriodId.Should().Be(fiscalPeriodId);
    }

    [Fact]
    [Trait("Category", "JournalApprovalWithdrawal")]
    public async Task WithdrawApproval_ShouldPersistDistinctMetadata_ReleaseBudget_AndNotifyPendingApprover()
    {
        var tenantId = Guid.NewGuid();
        var withdrawingUserId = Guid.NewGuid();
        var approverId = Guid.NewGuid();
        await using var db = CreateContext();
        var (debitAccount, creditAccount) = await SeedTenantPeriodAndAccountsAsync(db, tenantId);
        var notifications = new Mock<INotificationService>();
        notifications
            .Setup(x => x.CreateInAppNotificationAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>?>(),
                tenantId))
            .Returns(Task.CompletedTask);
        var budgetControl = new Mock<IFinanceBudgetControlService>();
        budgetControl
            .Setup(x => x.ReleaseManualJournalAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateJournalService(
            db,
            tenantId,
            withdrawingUserId,
            notifications,
            budgetControl.Object);
        var journal = await service.CreateJournalEntryAsync(CreateJournalDto(debitAccount.Id, creditAccount.Id));
        await service.UpdateApprovalStatusAsync(journal.Id, "Pending Approval", "Pending");

        var entityType = new WorkflowEntityType
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "JournalEntry", Name = "Journal Entry"
        };
        var definition = new WorkflowDefinition
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Name = "Journal approval", EntityTypeId = entityType.Id
        };
        var step = new WorkflowStep
        {
            Id = Guid.NewGuid(), TenantId = tenantId, WorkflowDefinitionId = definition.Id,
            Name = "Finance approval", StepType = WorkflowStepType.Approval, Order = 1
        };
        var instance = new WorkflowInstance
        {
            Id = Guid.NewGuid(), TenantId = tenantId, WorkflowDefinitionId = definition.Id,
            EntityTypeId = entityType.Id, EntityId = journal.Id, InitiatedById = withdrawingUserId,
            Status = WorkflowInstanceStatus.Cancelled, CompletedDate = DateTime.UtcNow
        };
        var stepInstance = new WorkflowStepInstance
        {
            Id = Guid.NewGuid(), TenantId = tenantId, WorkflowInstanceId = instance.Id,
            WorkflowStepId = step.Id, Status = WorkflowStepInstanceStatus.Cancelled,
            AssignedToId = approverId
        };
        db.AddRange(entityType, definition, step, instance, stepInstance);
        db.WorkflowApprovals.Add(new WorkflowApproval
        {
            Id = Guid.NewGuid(), TenantId = tenantId, StepInstanceId = stepInstance.Id,
            ApproverId = approverId, Status = WorkflowApprovalStatus.Expired
        });
        await db.SaveChangesAsync();

        const string reason = "Incorrect supporting schedule selected.";
        await service.WithdrawApprovalAsync(journal.Id, withdrawingUserId, reason);

        var stored = await db.JournalEntries.SingleAsync(x => x.Id == journal.Id);
        stored.PostingStatus.Should().Be("Draft");
        stored.ApprovalStatus.Should().Be("Withdrawn");
        stored.RejectionReason.Should().BeNull();
        stored.WithdrawalReason.Should().Be(reason);
        stored.WithdrawnByUserId.Should().Be(withdrawingUserId);
        stored.WithdrawnDate.Should().NotBeNull();
        budgetControl.Verify(
            x => x.ReleaseManualJournalAsync(journal.Id, reason, It.IsAny<CancellationToken>()),
            Times.Once);
        notifications.Verify(
            x => x.CreateInAppNotificationAsync(
                approverId,
                "Journal approval request withdrawn",
                It.Is<string>(message => message.Contains(reason)),
                "FinanceJournalApprovalWithdrawn",
                It.IsAny<Dictionary<string, object>?>(),
                tenantId),
            Times.Once);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"journal-lifecycle-batch5-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static JournalEntryService CreateJournalService(
        ApplicationDbContext db,
        Guid tenantId,
        Guid? currentUserId = null,
        Mock<INotificationService>? notification = null,
        IFinanceBudgetControlService? budgetControl = null,
        IWorkflowService? approvalWorkflow = null)
    {
        var currentUser = CreateCurrentUser(tenantId, currentUserId);
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

        notification ??= new Mock<INotificationService>();
        var books = new Mock<IAccountingBookService>();
        if (budgetControl is null)
        {
            var unbudgetedJournal = new Mock<IFinanceBudgetControlService>();
            unbudgetedJournal.Setup(control => control.ValidateManualJournalForPostingAsync(
                    It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<Guid>());
            budgetControl = unbudgetedJournal.Object;
        }

        return new JournalEntryService(
            db,
            currentUser.Object,
            gl.Object,
            audit.Object,
            notification.Object,
            books.Object,
            engine,
            budgetControl: budgetControl,
            approvalWorkflow: approvalWorkflow);
    }

    private static Mock<ICurrentUserService> CreateCurrentUser(Guid tenantId, Guid? currentUserId = null)
    {
        var userId = currentUserId ?? Guid.NewGuid();
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
        var period = SeedPeriod(db, tenantId);
        var debitAccount = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var creditAccount = SeedAccount(db, tenantId, "4000", AccountType.Revenue);
        var book = new AccountingBook
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "IFRS", Name = "IFRS Primary",
            IsDefault = true, IsActive = true, AllowsPosting = true
        };
        var assetClassification = new AccountClassification
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountingBookId = book.Id,
            Code = "ASSET_TEST", Name = "Asset test", CoreAccountType = AccountType.Asset,
            IsPostingClassification = true, Status = AccountClassificationStatus.Active
        };
        var revenueClassification = new AccountClassification
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountingBookId = book.Id,
            Code = "REVENUE_TEST", Name = "Revenue test", CoreAccountType = AccountType.Revenue,
            IsPostingClassification = true, Status = AccountClassificationStatus.Active
        };
        db.AddRange(book, assetClassification, revenueClassification);
        db.AccountAccountingBooks.AddRange(
            new AccountAccountingBook
            {
                Id = Guid.NewGuid(), TenantId = tenantId, AccountId = debitAccount.Id,
                AccountingBookId = book.Id, AccountClassificationId = assetClassification.Id, IsEnabled = true
            },
            new AccountAccountingBook
            {
                Id = Guid.NewGuid(), TenantId = tenantId, AccountId = creditAccount.Id,
                AccountingBookId = book.Id, AccountClassificationId = revenueClassification.Id, IsEnabled = true
            });
        FinancePostingAuthorityFixture.SeedExactBookPeriod(db, tenantId, period, book.Code);
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
        var fiscalYear = new FiscalYear
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearName = "Fiscal Year 2026",
            FiscalYearCode = $"FY26-{tenantId.ToString("N")[..4]}",
            Year = 2026,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31),
            TotalDays = 365,
            NumberOfPeriods = 12,
            Status = "Open",
            IsActive = true
        };
        var period = new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearId = fiscalYear.Id,
            FiscalYear = fiscalYear,
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

        db.FiscalYears.Add(fiscalYear);
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
        var book = db.AccountingBooks.Local.Single(item => item.TenantId == tenantId && item.Code == "IFRS");
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
            AccountingBookId = book.Id,
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
            AccountingBookId = book.Id,
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
            AccountingBookId = book.Id,
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
