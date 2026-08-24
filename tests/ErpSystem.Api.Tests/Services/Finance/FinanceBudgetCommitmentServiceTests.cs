using ErpSystem.Api.Services.Finance.Budget;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceBudgetCommitmentServiceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();

    [Fact]
    public async Task Position_uses_adopted_budget_posted_actuals_and_active_reservations()
    {
        await using var db = CreateContext();
        var fixture = SeedBudget(db, 1_000m);
        SeedPostedActual(db, fixture, 125m);
        db.FinanceBudgetReservations.Add(Reservation(fixture, Guid.NewGuid(), 200m));
        await db.SaveChangesAsync();

        var position = await CreateService(db).GetBudgetPositionAsync(fixture.Entry.Id, fixture.BudgetDate);

        position.ApprovedAmount.Should().Be(1_000m);
        position.PostedActualAmount.Should().Be(125m);
        position.ReservedAmount.Should().Be(200m);
        position.AvailableAmount.Should().Be(675m);
    }

    [Fact]
    public async Task Reserve_aggregates_source_lines_and_replays_without_duplicate_exposure()
    {
        await using var db = CreateContext();
        var fixture = SeedBudget(db, 1_000m);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var request = Request(fixture, "reserve-1", 200m, 150m);

        var first = await service.ReserveAsync(request);
        var replay = await service.ReserveAsync(request);

        first.IdempotentReplay.Should().BeFalse();
        first.Reservations.Should().ContainSingle().Which.FunctionalAmount.Should().Be(350m);
        first.Reservations.Single().SourceLineIds.Should().BeEquivalentTo("REQ-L1", "REQ-L2");
        replay.IdempotentReplay.Should().BeTrue();
        replay.Reservations.Single().Id.Should().Be(first.Reservations.Single().Id);
        (await db.FinanceBudgetReservations.CountAsync()).Should().Be(1);
        (await db.FinanceBudgetReservationOperations.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Reusing_idempotency_key_with_changed_payload_fails_closed()
    {
        await using var db = CreateContext();
        var fixture = SeedBudget(db, 1_000m);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        await service.ReserveAsync(Request(fixture, "reserve-conflict", 200m));
        var changed = Request(fixture, "reserve-conflict", 201m);

        var act = () => service.ReserveAsync(changed);

        var error = await act.Should().ThrowAsync<FinanceBudgetCommitmentConflictException>();
        error.Which.Code.Should().Be("BUDGET_IDEMPOTENCY_CONFLICT");
        (await db.FinanceBudgetReservations.SingleAsync()).ReservedAmount.Should().Be(200m);
    }

    [Fact]
    public async Task Target_state_adjustment_and_release_are_versioned_and_idempotent()
    {
        await using var db = CreateContext();
        var fixture = SeedBudget(db, 1_000m);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var reserved = (await service.ReserveAsync(Request(fixture, "reserve-adjust", 200m))).Reservations.Single();
        var set = new SetFinanceBudgetReservationAmountDto
        {
            DesiredTransactionAmount = 275m,
            ExpectedVersion = reserved.Version,
            SourceVersion = "REQ-V2",
            IdempotencyKey = "set-1",
            CorrelationId = "corr-set-1"
        };

        var adjusted = await service.SetReservationAmountAsync(reserved.Id, set);
        var replay = await service.SetReservationAmountAsync(reserved.Id, set);
        var released = await service.ReleaseAsync(reserved.Id, new ReleaseFinanceBudgetReservationDto
        {
            ExpectedVersion = adjusted.Version,
            Reason = "Requisition cancelled by its owning workflow",
            IdempotencyKey = "release-1",
            CorrelationId = "corr-release-1"
        });

        adjusted.FunctionalAmount.Should().Be(275m);
        adjusted.Version.Should().Be(2);
        replay.IdempotentReplay.Should().BeTrue();
        replay.Version.Should().Be(2);
        released.Status.Should().Be("Released");
        released.Version.Should().Be(3);
    }

    [Fact]
    public async Task Foreign_currency_requires_exact_approved_finance_rate()
    {
        await using var db = CreateContext();
        var fixture = SeedBudget(db, 1_000m);
        await db.SaveChangesAsync();
        var rateId = Guid.NewGuid();
        var exchangeRates = new Mock<IExchangeRateService>();
        exchangeRates.Setup(x => x.GetExchangeRateByIdAsync(rateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExchangeRateDto
            {
                Id = rateId,
                TenantId = TenantId,
                BaseCurrencyCode = "USD",
                TargetCurrencyCode = "GHS",
                Rate = 12m,
                InverseRate = 1m / 12m,
                EffectiveDate = fixture.Period.StartDate,
                IsActive = true,
                ApprovalStatus = "Approved"
            });
        var request = Request(fixture, "reserve-usd", 50m);
        request.Lines[0].TransactionCurrencyCode = "USD";
        request.Lines[0].ExchangeRateId = rateId;

        var result = await CreateService(db, exchangeRates.Object).ReserveAsync(request);

        result.Reservations.Single().TransactionAmount.Should().Be(50m);
        result.Reservations.Single().FunctionalAmount.Should().Be(600m);
        result.Reservations.Single().FunctionalCurrencyCode.Should().Be("GHS");
    }

    [Fact]
    public async Task Posting_outcome_requires_exact_related_source_action_and_never_writes_actuals()
    {
        await using var db = CreateContext();
        var fixture = SeedBudget(db, 1_000m);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var reserved = (await service.ReserveAsync(Request(fixture, "reserve-post", 300m))).Reservations.Single();
        var journal = PostedJournal(fixture, 100m);
        var receiptId = Guid.NewGuid();
        var receiptEvent = PostingEvent(journal, Guid.NewGuid(), "ProcurementPurchaseOrderReceipt", receiptId);
        receiptEvent.PostingAction = "PostAcceptedReceipt";
        db.AddRange(journal, receiptEvent);
        await db.SaveChangesAsync();

        var wrongRequest = PostingOutcome(receiptEvent, journal, 200m, reserved.Version, "post-wrong");
        wrongRequest.PostingAction = "ReverseAcceptedReceipt";
        var wrong = () => service.ApplyPostingOutcomeAsync(reserved.Id, wrongRequest);
        var error = await wrong.Should().ThrowAsync<FinanceBudgetCommitmentNotFoundException>();
        error.Which.Code.Should().Be("BUDGET_POSTING_EVENT_NOT_FOUND");

        var outcome = await service.ApplyPostingOutcomeAsync(
            reserved.Id, PostingOutcome(receiptEvent, journal, 200m, reserved.Version, "post-exact"));

        outcome.Status.Should().Be("Reserved");
        outcome.FunctionalAmount.Should().Be(200m);
        outcome.JournalEntryId.Should().Be(journal.Id);
        (await db.AccountTransactions.Where(x => x.JournalEntryId == journal.Id).SumAsync(x => x.DebitAmount)).Should().Be(100m);
    }

    [Fact]
    public async Task Cross_tenant_budget_entry_is_concealed()
    {
        await using var db = CreateContext();
        var other = SeedBudget(db, 1_000m, Guid.NewGuid());
        db.FinanceSettings.Add(new FinanceSettings
        {
            TenantId = TenantId,
            BaseCurrency = "GHS",
            CoaType = "Segmented",
            AccountSeparator = "-"
        });
        await db.SaveChangesAsync();

        var act = () => CreateService(db).EvaluateAsync(Request(other, "cross-tenant", 50m));

        var error = await act.Should().ThrowAsync<FinanceBudgetCommitmentNotFoundException>();
        error.Which.Code.Should().Be("BUDGET_ENTRY_NOT_FOUND");
    }

    [Fact]
    public void Migration_adds_versioned_operation_evidence_and_reverses_cleanly()
    {
        var up = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        new TestableCommitmentMigration().ApplyUp(up);

        up.Operations.OfType<CreateTableOperation>().Should().ContainSingle(x =>
            x.Name == "FinanceBudgetReservationOperations"
            && x.Columns.Any(column => column.Name == "IdempotencyKey" && column.MaxLength == 100));
        up.Operations.OfType<AddColumnOperation>().Select(x => x.Name).Should().BeEquivalentTo(
            "BudgetDate", "ExchangeRate", "ExchangeRateId", "ReservationVersion",
            "SourceDocumentReference", "SourceLineIdsJson", "SourceVersion",
            "TransactionAmount", "TransactionCurrencyCode");
        up.Operations.OfType<CreateIndexOperation>().Should().Contain(x =>
            x.Name == "IX_FinanceBudgetReservationOperations_TenantId_IdempotencyKey"
            && x.IsUnique && x.Filter == "[IsDeleted] = 0");
        up.Operations.OfType<SqlOperation>().Should().ContainSingle(x =>
            x.Sql.Contains("SET [TransactionCurrencyCode] = [CurrencyCode]", StringComparison.Ordinal));
        up.Operations.OfType<AddCheckConstraintOperation>().Select(x => x.Name).Should().BeEquivalentTo(
            "CK_FinanceBudgetReservations_TransactionCurrencyCode",
            "CK_FinanceBudgetReservations_TransactionAmount",
            "CK_FinanceBudgetReservations_ExchangeRate",
            "CK_FinanceBudgetReservations_ReservationVersion");

        var down = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        new TestableCommitmentMigration().ApplyDown(down);
        down.Operations.OfType<DropTableOperation>().Should().ContainSingle(x =>
            x.Name == "FinanceBudgetReservationOperations");
        down.Operations.OfType<DropColumnOperation>().Should().HaveCount(9);
        down.Operations.OfType<DropCheckConstraintOperation>().Should().HaveCount(4);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BudgetCommitmentTestDbContext>()
            .UseInMemoryDatabase($"finance-budget-commitment-{Guid.NewGuid():N}")
            .Options;
        return new BudgetCommitmentTestDbContext(options);
    }

    private static FinanceBudgetCommitmentService CreateService(
        ApplicationDbContext db,
        IExchangeRateService? exchangeRates = null)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(TenantId);
        currentUser.SetupGet(x => x.UserId).Returns(ActorId.ToString());
        currentUser.SetupGet(x => x.UserName).Returns("finance.budget.contract");
        return new FinanceBudgetCommitmentService(db, currentUser.Object, exchangeRates ?? Mock.Of<IExchangeRateService>());
    }

    private static BudgetFixture SeedBudget(ApplicationDbContext db, decimal amount, Guid? tenantId = null)
    {
        var tenant = tenantId ?? TenantId;
        var year = new FiscalYear
        {
            TenantId = tenant,
            FiscalYearName = "FY2026",
            FiscalYearCode = $"FY2026-{tenant:N}",
            Year = 2026,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31)
        };
        var period = new FiscalPeriod
        {
            TenantId = tenant,
            FiscalYearId = year.Id,
            PeriodName = "January 2026",
            PeriodCode = "2026-01",
            PeriodNumber = 1,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, 31),
            IsOpen = true
        };
        var expense = new Account
        {
            TenantId = tenant,
            AccountCode = $"EXP-{tenant:N}",
            AccountNumber = "5000-FIN",
            AccountName = "Controlled operating expense",
            AccountType = AccountType.Expense,
            BudgetTrackingEnabled = true
        };
        var scenario = new BudgetScenario
        {
            TenantId = tenant,
            FiscalYearId = year.Id,
            Name = "FY2026 Adopted",
            Status = "Approved",
            IsActive = true,
            AdoptedAt = new DateTime(2025, 12, 20),
            AdoptionEffectiveDate = new DateTime(2026, 1, 1),
            VersionNumber = 1
        };
        var budgetReturn = new BudgetReturn
        {
            TenantId = tenant,
            BudgetScenarioId = scenario.Id,
            Status = "Approved",
            ApprovedDate = new DateTime(2025, 12, 20)
        };
        var entry = new BudgetEntry
        {
            TenantId = tenant,
            BudgetReturnId = budgetReturn.Id,
            AccountId = expense.Id,
            FiscalPeriodId = period.Id,
            Amount = amount,
            AmountBase = amount,
            CurrencyCode = "GHS",
            ExchangeRate = 1m
        };
        db.AddRange(year, period, expense, scenario, budgetReturn, entry);
        if (tenant == TenantId)
            db.FinanceSettings.Add(new FinanceSettings
            {
                TenantId = tenant,
                BaseCurrency = "GHS",
                CoaType = "Segmented",
                AccountSeparator = "-"
            });
        return new BudgetFixture(period, expense, scenario, budgetReturn, entry, new DateTime(2026, 1, 15));
    }

    private static FinanceBudgetCommitmentRequestDto Request(
        BudgetFixture fixture,
        string idempotencyKey,
        params decimal[] amounts)
    {
        var sourceId = Guid.Parse("6F943747-5B62-4075-A95B-16CBEEFF0110");
        return new FinanceBudgetCommitmentRequestDto
        {
            SourceDocumentType = "ProcurementRequisition",
            SourceDocumentId = sourceId,
            SourceDocumentReference = "PR-2026-0001",
            SourceVersion = "REQ-V1",
            BudgetDate = fixture.BudgetDate,
            IdempotencyKey = idempotencyKey,
            CorrelationId = $"corr-{idempotencyKey}",
            Lines = amounts.Select((amount, index) => new FinanceBudgetCommitmentLineDto
            {
                SourceLineId = $"REQ-L{index + 1}",
                BudgetEntryId = fixture.Entry.Id,
                AccountId = fixture.Expense.Id,
                FiscalPeriodId = fixture.Period.Id,
                TransactionAmount = amount,
                TransactionCurrencyCode = "GHS"
            }).ToList()
        };
    }

    private static FinanceBudgetReservation Reservation(BudgetFixture fixture, Guid sourceId, decimal amount) => new()
    {
        TenantId = TenantId,
        BudgetScenarioId = fixture.Scenario.Id,
        BudgetReturnId = fixture.Return.Id,
        BudgetEntryId = fixture.Entry.Id,
        AccountId = fixture.Expense.Id,
        FiscalPeriodId = fixture.Period.Id,
        CurrencyCode = "GHS",
        SourceDocumentType = "OtherControlledSource",
        SourceDocumentId = sourceId,
        SourceVersion = "V1",
        BudgetDate = fixture.BudgetDate,
        TransactionCurrencyCode = "GHS",
        TransactionAmount = amount,
        ExchangeRate = 1m,
        ReservationVersion = 1,
        ReservedAmount = amount,
        Status = "Reserved",
        EvaluationHash = new string('A', 64),
        ReservedByUserId = ActorId,
        ReservedAt = DateTime.UtcNow
    };

    private static void SeedPostedActual(ApplicationDbContext db, BudgetFixture fixture, decimal amount)
    {
        db.JournalEntries.Add(PostedJournal(fixture, amount));
    }

    private static JournalEntry PostedJournal(BudgetFixture fixture, decimal amount) => new()
    {
        TenantId = TenantId,
        JournalEntryNumber = $"JE-{Guid.NewGuid():N}",
        EntryDate = fixture.BudgetDate,
        FiscalPeriodId = fixture.Period.Id,
        JournalType = "General",
        Description = "Posted controlled expense",
        PostingStatus = "Posted",
        BookClassification = "IFRS",
        TotalDebitAmount = amount,
        TotalCreditAmount = amount,
        IsBalanced = true,
        Transactions = new List<AccountTransaction>
        {
            new()
            {
                TenantId = TenantId,
                AccountId = fixture.Expense.Id,
                FiscalPeriodId = fixture.Period.Id,
                TransactionDate = fixture.BudgetDate,
                DebitAmount = amount,
                LineNumber = 1
            }
        }
    };

    private static FinancePostingEvent PostingEvent(
        JournalEntry journal,
        Guid eventId,
        string sourceType,
        Guid sourceId) => new()
    {
        Id = eventId,
        TenantId = TenantId,
        SourceModule = "PROCUREMENT",
        SourceDocumentType = sourceType,
        SourceDocumentId = sourceId,
        PostingAction = "Post",
        JournalEntryId = journal.Id,
        PostingStatus = "Posted",
        PostingDate = journal.EntryDate,
        PostedAt = DateTime.UtcNow,
        FunctionalCurrencyCode = "GHS",
        BookClassification = "IFRS"
    };

    private static ApplyFinanceBudgetPostingOutcomeDto PostingOutcome(
        FinancePostingEvent postingEvent,
        JournalEntry journal,
        decimal remaining,
        int version,
        string key) => new()
    {
        PostingEventId = postingEvent.Id,
        JournalEntryId = journal.Id,
        RemainingTransactionAmount = remaining,
        ExpectedVersion = version,
        IdempotencyKey = key,
        CorrelationId = $"corr-{key}",
        PostingSourceDocumentType = postingEvent.SourceDocumentType,
        PostingSourceDocumentId = postingEvent.SourceDocumentId,
        PostingAction = postingEvent.PostingAction
    };

    private sealed record BudgetFixture(
        FiscalPeriod Period,
        Account Expense,
        BudgetScenario Scenario,
        BudgetReturn Return,
        BudgetEntry Entry,
        DateTime BudgetDate);

    private sealed class TestableCommitmentMigration : AddGenericFinanceBudgetCommitmentContract
    {
        public void ApplyUp(MigrationBuilder builder) => Up(builder);
        public void ApplyDown(MigrationBuilder builder) => Down(builder);
    }

    /// <summary>
    /// ApplicationDbContext currently embeds a constructor tenant in its cached EF model. A
    /// dedicated derived type gives this suite an unfiltered model so parallel tests with another
    /// tenant cannot alter its query results; the service's explicit tenant predicates remain the
    /// security assertion under test.
    /// </summary>
    private sealed class BudgetCommitmentTestDbContext(
        DbContextOptions<BudgetCommitmentTestDbContext> options) : ApplicationDbContext(options);
}
