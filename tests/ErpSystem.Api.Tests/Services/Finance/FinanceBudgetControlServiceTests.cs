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
        result.Lines.Single().Message.Should().Contain("department/cost-centre");
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
        var up = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        new TestableBudgetControlMigration().ApplyUp(up);

        up.Operations.OfType<CreateTableOperation>().Select(x => x.Name).Should().Equal(
            "FinanceBudgetOverrideRequests",
            "FinanceBudgetReservations");
        up.Operations.OfType<CreateTableOperation>().Should().OnlyContain(x =>
            x.Columns.Any(column => column.Name == "CurrencyCode" && column.MaxLength == 3 && !column.IsNullable));
        up.Operations.Should().OnlyContain(x =>
            x.GetType() == typeof(CreateTableOperation) || x.GetType() == typeof(CreateIndexOperation));
        up.Operations.OfType<CreateIndexOperation>().Should().Contain(x =>
            x.Name == "IX_FinanceBudgetOverrideRequests_WorkflowInstanceId"
            && x.IsUnique
            && x.Filter == "[WorkflowInstanceId] IS NOT NULL");
        up.Operations.OfType<CreateIndexOperation>().Should().Contain(x =>
            x.Name == "IX_FinanceBudgetReservations_TenantId_SourceDocumentType_SourceDocumentId_BudgetEntryId"
            && x.IsUnique
            && x.Filter == "[IsDeleted] = 0 AND [Status] = 'Reserved'");

        var down = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        new TestableBudgetControlMigration().ApplyDown(down);
        down.Operations.OfType<DropTableOperation>().Select(x => x.Name).Should().Equal(
            "FinanceBudgetReservations",
            "FinanceBudgetOverrideRequests");
        down.Operations.Should().HaveCount(2);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"finance-budget-control-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options, TenantId);
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
            TotalDebitAmount = 600m,
            TotalCreditAmount = 600m,
            IsBalanced = true,
            Transactions = new List<AccountTransaction>
            {
                new() { TenantId = TenantId, AccountId = expense.Id, TransactionDate = new DateTime(2026, 1, 15), DebitAmount = 600m, CreditAmount = 0m, LineNumber = 1 },
                new() { TenantId = TenantId, AccountId = clearing.Id, TransactionDate = new DateTime(2026, 1, 15), DebitAmount = 0m, CreditAmount = 600m, LineNumber = 2 }
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
