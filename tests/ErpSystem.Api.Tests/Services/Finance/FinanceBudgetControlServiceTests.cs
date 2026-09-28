using ErpSystem.Api.Services.Finance.Budget;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceBudgetControlServiceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task Untracked_expense_account_is_outside_control()
    {
        await using var db = CreateContext();
        var fixture = SeedJournal(db, budgetTrackingEnabled: false);
        await db.SaveChangesAsync();

        var result = await CreateService(db).EvaluateManualJournalAsync(fixture.Journal.Id);

        result.HasTrackedExpenseLines.Should().BeFalse();
        result.IsAllowed.Should().BeTrue();
        result.Lines.Should().BeEmpty();
    }

    [Fact]
    public async Task Tracked_expense_without_adopted_budget_fails_closed()
    {
        await using var db = CreateContext();
        var fixture = SeedJournal(db, budgetTrackingEnabled: true);
        await db.SaveChangesAsync();

        var result = await CreateService(db).EvaluateManualJournalAsync(fixture.Journal.Id);

        result.IsAllowed.Should().BeFalse();
        result.RequiresOverride.Should().BeFalse();
        result.Lines.Single().DecisionCode.Should().Be("NO_ADOPTED_BUDGET");
    }

    [Fact]
    public async Task Exact_adopted_budget_cell_reserves_and_releases_available_amount()
    {
        await using var db = CreateContext();
        var fixture = SeedJournal(db, budgetTrackingEnabled: true);
        SeedAdoptedBudget(db, fixture, 1_000m);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var evaluation = await service.EvaluateManualJournalAsync(fixture.Journal.Id);
        var reservationIds = await service.ReserveManualJournalAsync(fixture.Journal.Id);

        evaluation.IsAllowed.Should().BeTrue();
        evaluation.Lines.Single().AvailableAmount.Should().Be(1_000m);
        reservationIds.Should().ContainSingle();
        (await db.FinanceBudgetReservations.SingleAsync()).ReservedAmount.Should().Be(600m);

        await service.ReleaseManualJournalAsync(fixture.Journal.Id, "Journal withdrawn");
        (await db.FinanceBudgetReservations.SingleAsync()).Status.Should().Be("Released");
    }

    [Fact]
    public async Task Other_pending_journal_reservation_reduces_available_budget()
    {
        await using var db = CreateContext();
        var fixture = SeedJournal(db, budgetTrackingEnabled: true);
        var budget = SeedAdoptedBudget(db, fixture, 1_000m);
        db.FinanceBudgetReservations.Add(new FinanceBudgetReservation
        {
            TenantId = TenantId,
            BudgetScenarioId = budget.Scenario.Id,
            BudgetReturnId = budget.Return.Id,
            BudgetEntryId = budget.Entry.Id,
            AccountId = fixture.Expense.Id,
            FiscalPeriodId = fixture.Period.Id,
            CurrencyCode = "GHS",
            SourceDocumentType = FinanceBudgetControlService.ManualJournalSource,
            SourceDocumentId = Guid.NewGuid(),
            ReservedAmount = 500m,
            Status = "Reserved",
            EvaluationHash = new string('A', 64),
            ReservedByUserId = UserId,
            ReservedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await CreateService(db).EvaluateManualJournalAsync(fixture.Journal.Id);

        result.IsAllowed.Should().BeFalse();
        result.RequiresOverride.Should().BeTrue();
        result.TotalShortfallAmount.Should().Be(100m);
        result.Lines.Single().DecisionCode.Should().Be("INSUFFICIENT_BUDGET");
    }

    [Fact]
    public async Task Department_budget_return_must_match_account_combination_segment()
    {
        await using var db = CreateContext();
        var fixture = SeedJournal(db, budgetTrackingEnabled: true);
        var structure = new AccountSegmentStructure
        {
            TenantId = TenantId,
            SegmentCode = "DEPT",
            SegmentName = "Department",
            SegmentLength = 3,
            SegmentPosition = 1,
            IsReportingDimension = true,
            IsNaturalAccount = false
        };
        var accountDepartment = new SegmentLookupValue
        {
            TenantId = TenantId,
            SegmentStructureId = structure.Id,
            SegmentValue = "FIN",
            Description = "Finance"
        };
        var wrongDepartment = new SegmentLookupValue
        {
            TenantId = TenantId,
            SegmentStructureId = structure.Id,
            SegmentValue = "OPS",
            Description = "Operations"
        };
        db.AddRange(structure, accountDepartment, wrongDepartment);
        db.AccountSegmentValues.Add(new AccountSegmentValue
        {
            TenantId = TenantId,
            AccountId = fixture.Expense.Id,
            SegmentStructureId = structure.Id,
            SegmentLookupValueId = accountDepartment.Id,
            SegmentValue = accountDepartment.SegmentValue,
            SegmentPosition = 1,
            EffectiveDate = fixture.Journal.EntryDate.AddDays(-1)
        });
        SeedAdoptedBudget(db, fixture, 1_000m, wrongDepartment.Id);
        await db.SaveChangesAsync();

        var result = await CreateService(db).EvaluateManualJournalAsync(fixture.Journal.Id);

        result.IsAllowed.Should().BeFalse();
        result.Lines.Single().DecisionCode.Should().Be("NO_MATCHING_BUDGET_LINE");
        result.Lines.Single().Message.Should().Contain("Department budget combination");
        result.Lines.Single().Message.Should().NotContain("cost-centre");
    }

    [Fact]
    public async Task Missing_budget_cell_names_the_adopted_scenario_control_dimensions()
    {
        await using var db = CreateContext();
        var fixture = SeedJournal(db, budgetTrackingEnabled: true);
        var budget = SeedAdoptedBudget(db, fixture, 1_000m);
        // Keep the adopted scenario but remove its only cell from consideration so the
        // diagnostic must describe the scenario grain rather than a successful match.
        budget.Entry.IsDeleted = true;
        var department = new FinanceDimensionDefinition
        {
            TenantId = TenantId,
            Code = "DEPT",
            Name = "Department",
            DisplayOrder = 1,
            IsActive = true
        };
        var project = new FinanceDimensionDefinition
        {
            TenantId = TenantId,
            Code = "PROJECT",
            Name = "Project",
            DisplayOrder = 2,
            IsActive = true
        };
        db.AddRange(department, project);
        db.AddRange(
            new BudgetScenarioControlDimension
            {
                TenantId = TenantId,
                BudgetScenarioId = budget.Scenario.Id,
                FinanceDimensionDefinitionId = department.Id,
                DisplayOrder = 1
            },
            new BudgetScenarioControlDimension
            {
                TenantId = TenantId,
                BudgetScenarioId = budget.Scenario.Id,
                FinanceDimensionDefinitionId = project.Id,
                DisplayOrder = 2
            });
        await db.SaveChangesAsync();

        var result = await CreateService(db).EvaluateManualJournalAsync(fixture.Journal.Id);

        result.IsAllowed.Should().BeFalse();
        result.Lines.Single().DecisionCode.Should().Be("NO_MATCHING_BUDGET_LINE");
        result.Lines.Single().Message.Should().Be(
            "No approved budget line matches this account's Department + Project budget combination for this fiscal period.");
    }

    [Fact]
    public async Task Posting_consumes_only_exact_reserved_evidence()
    {
        await using var db = CreateContext();
        var fixture = SeedJournal(db, budgetTrackingEnabled: true);
        SeedAdoptedBudget(db, fixture, 1_000m);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var ids = await service.ReserveManualJournalAsync(fixture.Journal.Id);
        var postingEventId = Guid.NewGuid();

        await service.ConsumeReservationsAsync(TenantId, fixture.Journal.Id, ids, fixture.Journal.Id, postingEventId);

        var reservation = await db.FinanceBudgetReservations.SingleAsync();
        reservation.Status.Should().Be("Consumed");
        reservation.JournalEntryId.Should().Be(fixture.Journal.Id);
        reservation.PostingEventId.Should().Be(postingEventId);
    }

    [Fact]
    public async Task Posted_journal_uses_immutable_budget_evidence_and_retains_approved_override()
    {
        await using var db = CreateContext();
        var fixture = SeedJournal(db, budgetTrackingEnabled: true);
        var budget = SeedAdoptedBudget(db, fixture, 1_000m);
        var clearingAccountId = fixture.Journal.Transactions.Single(x => x.CreditAmount > 0).AccountId;
        var priorJournal = new JournalEntry
        {
            TenantId = TenantId,
            JournalEntryNumber = "JE-2026-PRIOR",
            EntryDate = fixture.Journal.EntryDate.AddDays(-1),
            FiscalPeriodId = fixture.Period.Id,
            JournalType = "General",
            Description = "Prior controlled expense",
            PostingStatus = "Posted",
            BookClassification = "IFRS",
            AccountingBookId = fixture.Journal.AccountingBookId,
            TotalDebitAmount = 500m,
            TotalCreditAmount = 500m,
            IsBalanced = true,
            Transactions = new List<AccountTransaction>
            {
                new() { TenantId = TenantId, AccountingBookId = fixture.Journal.AccountingBookId, AccountId = fixture.Expense.Id, FiscalPeriodId = fixture.Period.Id, TransactionDate = fixture.Journal.EntryDate.AddDays(-1), DebitAmount = 500m, LineNumber = 1 },
                new() { TenantId = TenantId, AccountingBookId = fixture.Journal.AccountingBookId, AccountId = clearingAccountId, FiscalPeriodId = fixture.Period.Id, TransactionDate = fixture.Journal.EntryDate.AddDays(-1), CreditAmount = 500m, LineNumber = 2 }
            }
        };
        db.JournalEntries.Add(priorJournal);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var beforePosting = await service.EvaluateManualJournalAsync(fixture.Journal.Id);
        beforePosting.TotalShortfallAmount.Should().Be(100m);
        db.FinanceBudgetOverrideRequests.Add(new FinanceBudgetOverrideRequest
        {
            TenantId = TenantId,
            SourceDocumentType = FinanceBudgetControlService.ManualJournalSource,
            SourceDocumentId = fixture.Journal.Id,
            CurrencyCode = "GHS",
            EvaluationHash = beforePosting.EvaluationHash,
            Reason = "Approved exceptional expenditure",
            RequestedAmount = beforePosting.TotalRequestedAmount,
            ShortfallAmount = beforePosting.TotalShortfallAmount,
            Status = "Approved",
            RequestedByUserId = UserId,
            RequestedAt = DateTime.UtcNow.AddMinutes(-5),
            ApprovedByUserId = Guid.NewGuid(),
            ApprovedAt = DateTime.UtcNow.AddMinutes(-1)
        });
        await db.SaveChangesAsync();
        var reservationIds = await service.ReserveManualJournalAsync(fixture.Journal.Id);
        var postingEventId = Guid.NewGuid();
        await service.ConsumeReservationsAsync(TenantId, fixture.Journal.Id, reservationIds, fixture.Journal.Id, postingEventId);
        fixture.Journal.PostingStatus = "Posted";
        await db.SaveChangesAsync();

        // Later actuals must not rewrite the historical decision evidence.
        priorJournal.Transactions.Single(x => x.DebitAmount > 0).DebitAmount = 900m;
        await db.SaveChangesAsync();
        var posted = await service.EvaluateManualJournalAsync(fixture.Journal.Id);

        posted.IsPostingSnapshot.Should().BeTrue();
        posted.EvaluationHash.Should().Be(beforePosting.EvaluationHash);
        posted.HasApprovedOverride.Should().BeTrue();
        posted.IsAllowed.Should().BeTrue();
        posted.TotalRequestedAmount.Should().Be(600m);
        posted.TotalShortfallAmount.Should().Be(100m);
        posted.Lines.Single().BudgetAmount.Should().Be(budget.Entry.AmountBase);
        posted.Lines.Single().PostedActualAmount.Should().Be(500m);
        posted.Lines.Single().AvailableAmount.Should().Be(500m);
    }

    [Fact]
    public async Task Override_approval_requires_completed_shared_workflow_evidence()
    {
        await using var db = CreateContext();
        var request = new FinanceBudgetOverrideRequest
        {
            TenantId = TenantId,
            SourceDocumentType = FinanceBudgetControlService.ManualJournalSource,
            SourceDocumentId = Guid.NewGuid(),
            CurrencyCode = "GHS",
            EvaluationHash = new string('B', 64),
            Reason = "Exceptional operational expenditure",
            RequestedAmount = 700m,
            ShortfallAmount = 100m,
            Status = "PendingApproval",
            WorkflowInstanceId = Guid.NewGuid(),
            RequestedByUserId = UserId,
            RequestedAt = DateTime.UtcNow
        };
        db.FinanceBudgetOverrideRequests.Add(request);
        db.WorkflowInstances.Add(new WorkflowInstance
        {
            Id = request.WorkflowInstanceId!.Value,
            TenantId = TenantId,
            WorkflowDefinitionId = Guid.NewGuid(),
            EntityTypeId = Guid.NewGuid(),
            EntityId = request.Id,
            InitiatedById = UserId,
            Status = WorkflowInstanceStatus.InProgress
        });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var act = () => service.ApplyOverrideOutcomeAsync(request.Id, true, Guid.NewGuid(), "approved");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not complete*");
        request.Status.Should().Be("PendingApproval");

        (await db.WorkflowInstances.SingleAsync()).Status = WorkflowInstanceStatus.Completed;
        await db.SaveChangesAsync();
        var approverId = Guid.NewGuid();
        await service.ApplyOverrideOutcomeAsync(request.Id, true, approverId, "approved");
        request.Status.Should().Be("Approved");
        request.ApprovedByUserId.Should().Be(approverId);
        request.ApprovedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Editing_source_journal_supersedes_override_and_cancels_pending_workflow()
    {
        await using var db = CreateContext();
        var fixture = SeedJournal(db, budgetTrackingEnabled: true);
        var pending = new FinanceBudgetOverrideRequest
        {
            TenantId = TenantId,
            SourceDocumentType = FinanceBudgetControlService.ManualJournalSource,
            SourceDocumentId = fixture.Journal.Id,
            CurrencyCode = "GHS",
            EvaluationHash = new string('C', 64),
            Reason = "Exceptional operational expenditure",
            RequestedAmount = 700m,
            ShortfallAmount = 100m,
            Status = "PendingApproval",
            WorkflowInstanceId = Guid.NewGuid(),
            RequestedByUserId = UserId,
            RequestedAt = DateTime.UtcNow
        };
        db.FinanceBudgetOverrideRequests.Add(pending);
        await db.SaveChangesAsync();
        var workflow = new Mock<IWorkflowService>();
        workflow.Setup(x => x.CancelWorkflowAsync(
                FinanceBudgetControlService.OverrideWorkflowType,
                pending.Id,
                It.IsAny<string>()))
            .ReturnsAsync(new WorkflowExecutionResult { Success = true, Status = WorkflowInstanceStatus.Cancelled });
        var service = CreateService(db, workflow.Object);

        await service.InvalidateManualJournalOverridesAsync(fixture.Journal.Id, "Journal lines changed");

        pending.Status.Should().Be("Superseded");
        pending.RejectionReason.Should().Be("Journal lines changed");
        workflow.Verify(x => x.CancelWorkflowAsync(
            FinanceBudgetControlService.OverrideWorkflowType,
            pending.Id,
            "Journal lines changed"), Times.Once);
    }

    [Fact]
    public void Migration_creates_only_budget_control_evidence_and_reverses_cleanly()
    {
        var source = ArchivedMigrationSource.Read("20260824080000_AddFinanceBudgetControlFoundation.cs");
        foreach (var token in new[]
        {
            "FinanceBudgetOverrideRequests",
            "FinanceBudgetReservations",
            "CurrencyCode",
            "maxLength: 3",
            "IX_FinanceBudgetOverrideRequests_WorkflowInstanceId",
            "[WorkflowInstanceId] IS NOT NULL",
            "IX_FinanceBudgetReservations_TenantId_SourceDocumentType_SourceDocumentId_BudgetEntryId",
            "[IsDeleted] = 0 AND [Status] = 'Reserved'",
            "migrationBuilder.DropTable"
        }) source.Should().Contain(token);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"finance-budget-control-{Guid.NewGuid():N}")
            .Options;
        var db = new ApplicationDbContext(options, TenantId);
        db.AccountingBooks.Add(new AccountingBook
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            Code = "PRIMARY", Name = "Primary book",
            BookType = AccountingBookType.PrimaryFull,
            IsDefault = true, IsActive = true, AllowsPosting = true
        });
        return db;
    }

    private static FinanceBudgetControlService CreateService(ApplicationDbContext db, IWorkflowService? workflow = null)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(TenantId);
        currentUser.SetupGet(x => x.UserId).Returns(UserId.ToString());
        currentUser.SetupGet(x => x.UserName).Returns("budget.test");
        return new FinanceBudgetControlService(db, currentUser.Object, workflow ?? Mock.Of<IWorkflowService>());
    }

    private static JournalFixture SeedJournal(ApplicationDbContext db, bool budgetTrackingEnabled)
    {
        var primaryBook = db.AccountingBooks.Local.Single(book => book.IsDefault);
        var fiscalYear = new FiscalYear
        {
            TenantId = TenantId,
            FiscalYearName = "FY2026",
            FiscalYearCode = "FY2026",
            Year = 2026,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31)
        };
        var period = new FiscalPeriod
        {
            TenantId = TenantId,
            FiscalYearId = fiscalYear.Id,
            PeriodName = "January 2026",
            PeriodCode = "2026-01",
            PeriodNumber = 1,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, 31),
            IsOpen = true
        };
        var expense = new Account
        {
            TenantId = TenantId,
            AccountCode = "EXP-FIN",
            AccountNumber = "5000-FIN",
            AccountName = "Finance operating expense",
            AccountType = AccountType.Expense,
            BudgetTrackingEnabled = budgetTrackingEnabled
        };
        var clearing = new Account
        {
            TenantId = TenantId,
            AccountCode = "CLR",
            AccountNumber = "2000",
            AccountName = "Accrued liabilities",
            AccountType = AccountType.Liability
        };
        var journal = new JournalEntry
        {
            TenantId = TenantId,
            JournalEntryNumber = "JE-2026-00001",
            EntryDate = new DateTime(2026, 1, 15),
            FiscalPeriodId = period.Id,
            JournalType = "General",
            Description = "Controlled expense",
            PostingStatus = "Draft",
            BookClassification = "IFRS",
            AccountingBookId = primaryBook.Id,
            TotalDebitAmount = 600m,
            TotalCreditAmount = 600m,
            IsBalanced = true,
            Transactions = new List<AccountTransaction>
            {
                new() { TenantId = TenantId, AccountingBookId = primaryBook.Id, AccountId = expense.Id, TransactionDate = new DateTime(2026, 1, 15), DebitAmount = 600m, CreditAmount = 0m, LineNumber = 1 },
                new() { TenantId = TenantId, AccountingBookId = primaryBook.Id, AccountId = clearing.Id, TransactionDate = new DateTime(2026, 1, 15), DebitAmount = 0m, CreditAmount = 600m, LineNumber = 2 }
            }
        };
        var financeSettings = new FinanceSettings
        {
            TenantId = TenantId,
            BaseCurrency = "GHS",
            CoaType = "Segmented",
            AccountSeparator = "-"
        };
        db.AddRange(fiscalYear, period, expense, clearing, journal, financeSettings);
        return new JournalFixture(fiscalYear, period, expense, journal);
    }

    private static BudgetFixture SeedAdoptedBudget(ApplicationDbContext db, JournalFixture fixture, decimal amount, Guid? segmentValueId = null)
    {
        var scenario = new BudgetScenario
        {
            TenantId = TenantId,
            FiscalYearId = fixture.FiscalYear.Id,
            Name = "FY2026 Adopted",
            Status = "Approved",
            IsActive = true,
            AdoptedAt = new DateTime(2025, 12, 20),
            AdoptionEffectiveDate = new DateTime(2026, 1, 1),
            VersionNumber = 1
        };
        var budgetReturn = new BudgetReturn
        {
            TenantId = TenantId,
            BudgetScenarioId = scenario.Id,
            SegmentValueId = segmentValueId,
            Status = "Approved",
            ApprovedDate = new DateTime(2025, 12, 19)
        };
        var entry = new BudgetEntry
        {
            TenantId = TenantId,
            BudgetReturnId = budgetReturn.Id,
            AccountId = fixture.Expense.Id,
            FiscalPeriodId = fixture.Period.Id,
            Amount = amount,
            AmountBase = amount,
            CurrencyCode = "GHS",
            ExchangeRate = 1m
        };
        db.AddRange(scenario, budgetReturn, entry);
        return new BudgetFixture(scenario, budgetReturn, entry);
    }

    private sealed record JournalFixture(FiscalYear FiscalYear, FiscalPeriod Period, Account Expense, JournalEntry Journal);
    private sealed record BudgetFixture(BudgetScenario Scenario, BudgetReturn Return, BudgetEntry Entry);

    private sealed class TestableBudgetControlMigration : AddFinanceBudgetControlFoundation
    {
        public void ApplyUp(MigrationBuilder builder) => Up(builder);
        public void ApplyDown(MigrationBuilder builder) => Down(builder);
    }
}
