using ErpSystem.Api.Tests.Services.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Medical;
using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR;
using ErpSystem.Core.Services.HR.Finance;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.HR;

/// <summary>
/// The HR → Finance consumer contract (FIN-INT-001, HR finish plan lane 8), in the shape
/// <c>docs/Finance/finance-integration-consumer-test-template.md</c> requires: happy path through
/// <see cref="FinanceConsumerContractAssertions.ShouldSatisfyPostingContract"/>, identical retry,
/// Finance-failure-does-not-mark-posted, wrong tenant / missing configuration, and the HR-specific
/// rules (unposted-when-disabled, skip, generations, foreign-currency evidence).
///
/// Every test drives the real adapter with the real command factory; only the store (HR's own
/// tables and Finance master-data reads), the unit of work, the engine and the currency services
/// are mocked. The captured <c>FinancePostingRequestV2Dto</c> is what Finance would receive.
/// </summary>
public sealed class HrFinancePostingAdapterTests
{
    private static readonly Guid Tenant = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid User = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid EmployeeId = new("22222222-2222-2222-2222-222222222222");

    private static readonly Guid PayableAccount = new("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid ReceivableAccount = new("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid ClearingAccount = new("aaaaaaaa-0000-0000-0000-000000000003");
    private static readonly Guid MedicalExpenseAccount = new("aaaaaaaa-0000-0000-0000-000000000004");
    private static readonly Guid TravelExpenseAccount = new("aaaaaaaa-0000-0000-0000-000000000005");

    // ── Harness ──────────────────────────────────────────────────────────────────────────────

    private sealed class Harness
    {
        public Mock<IHrFinancePostingStore> Store { get; } = new(MockBehavior.Strict);
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public Mock<IFinancePostingEngine> Engine { get; } = new(MockBehavior.Strict);
        public Mock<ICurrencyService> Currencies { get; } = new();
        public Mock<IExchangeRateService> Rates { get; } = new();
        public Mock<ICurrentUserProvider> CurrentUser { get; } = new();

        public List<HrFinancePostingRecord> Added { get; } = new();
        public List<HrFinancePostingRecord> Updated { get; } = new();
        public List<FinancePostingRequestV2Dto> Sent { get; } = new();
        public int Saves { get; private set; }
        public int Begins { get; private set; }
        public int Commits { get; private set; }
        public int Rollbacks { get; private set; }
        public int Clears { get; private set; }

        public Dictionary<HrFinanceAccountRole, Guid> Mappings { get; } = new()
        {
            [HrFinanceAccountRole.StaffClaimsPayable] = PayableAccount,
            [HrFinanceAccountRole.StaffAdvancesReceivable] = ReceivableAccount,
            [HrFinanceAccountRole.StaffPaymentsClearing] = ClearingAccount,
            [HrFinanceAccountRole.MedicalExpense] = MedicalExpenseAccount,
            [HrFinanceAccountRole.TravelExpense] = TravelExpenseAccount
        };

        public Dictionary<Guid, HrFinanceAccountSnapshot> Accounts { get; } = new()
        {
            [PayableAccount] = new(PayableAccount, "2120", "000-2120-0000", "Accrued Payroll Payables", AccountType.Liability, true),
            [ReceivableAccount] = new(ReceivableAccount, "1120", "000-1120-0000", "Staff Loans and Salary Advances", AccountType.Asset, true),
            [ClearingAccount] = new(ClearingAccount, "1010", "000-1010-0000", "Cash and Bank - Payroll Clearing", AccountType.Asset, true),
            [MedicalExpenseAccount] = new(MedicalExpenseAccount, "6020", "000-6020-0000", "Salaries, Wages and Payroll Costs", AccountType.Expense, true),
            [TravelExpenseAccount] = new(TravelExpenseAccount, "6000", "000-6000-0000", "Salaries and Wages", AccountType.Expense, true)
        };

        public HrFinancePostingRule? Rule { get; set; } = new() { TenantId = Tenant, EventCode = "*", IsEnabled = true };
        public HrFinancePostingRecord? Existing { get; set; }
        public HrFinanceTenantContext Context { get; set; } = new("GHS", "IFRS", null);
        public Exception? EngineFailure { get; set; }

        public Harness()
        {
            CurrentUser.SetupGet(u => u.TenantId).Returns(Tenant);
            CurrentUser.SetupGet(u => u.UserId).Returns(User);

            UnitOfWork.SetupGet(u => u.HasActiveTransaction).Returns(false);
            UnitOfWork.Setup(u => u.ExecuteInStrategyAsync(It.IsAny<Func<Task<HrFinancePostingOutcome?>>>(), It.IsAny<CancellationToken>()))
                .Returns<Func<Task<HrFinancePostingOutcome?>>, CancellationToken>((f, _) => f());
            UnitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).Callback(() => Begins++).Returns(Task.CompletedTask);
            UnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).Callback(() => Commits++).Returns(Task.CompletedTask);
            UnitOfWork.Setup(u => u.RollbackAsync(It.IsAny<CancellationToken>())).Callback(() => Rollbacks++).Returns(Task.CompletedTask);
            UnitOfWork.Setup(u => u.ClearTrackedChanges()).Callback(() => Clears++);

            Store.Setup(s => s.GetTenantContextAsync(Tenant, It.IsAny<CancellationToken>())).ReturnsAsync(() => Context);
            Store.Setup(s => s.GetRuleAsync(Tenant, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid _, string code, CancellationToken _) => Rule is null ? null : new HrFinancePostingRule
                {
                    TenantId = Tenant, EventCode = code, IsEnabled = Rule.IsEnabled, PostOnActionDate = Rule.PostOnActionDate
                });
            Store.Setup(s => s.GetMappingsAsync(Tenant, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Mappings.Select(kv => new HrFinanceAccountMapping { TenantId = Tenant, Role = kv.Key, AccountId = kv.Value }).ToList());
            Store.Setup(s => s.GetAccountsAsync(Tenant, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid _, IReadOnlyCollection<Guid> ids, CancellationToken _) =>
                    (IReadOnlyDictionary<Guid, HrFinanceAccountSnapshot>)ids.Where(Accounts.ContainsKey).Distinct().ToDictionary(id => id, id => Accounts[id]));
            Store.Setup(s => s.FindRecordAsync(Tenant, It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Existing);
            Store.Setup(s => s.FindPostedRecordAsync(Tenant, It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Existing?.Status == HrFinancePostingStatus.Posted ? Existing : null);
            Store.Setup(s => s.AddRecordAsync(It.IsAny<HrFinancePostingRecord>(), It.IsAny<CancellationToken>()))
                .Callback<HrFinancePostingRecord, CancellationToken>((r, _) => Added.Add(r)).Returns(Task.CompletedTask);
            Store.Setup(s => s.UpdateRecordAsync(It.IsAny<HrFinancePostingRecord>(), It.IsAny<CancellationToken>()))
                .Callback<HrFinancePostingRecord, CancellationToken>((r, _) => Updated.Add(r)).Returns(Task.CompletedTask);
            Store.Setup(s => s.SaveChangesAsync(It.IsAny<CancellationToken>())).Callback(() => Saves++).Returns(Task.CompletedTask);

            Engine.Setup(e => e.PostAsync(It.IsAny<FinancePostingRequestV2Dto>(), It.IsAny<CancellationToken>()))
                .Returns<FinancePostingRequestV2Dto, CancellationToken>((request, _) =>
                {
                    Sent.Add(request);
                    if (EngineFailure is not null) throw EngineFailure;
                    return Task.FromResult(new FinancePostingResultDto
                    {
                        PostingEventId = Guid.NewGuid(),
                        JournalEntryId = Guid.NewGuid(),
                        JournalEntryNumber = $"JE-HR-{Sent.Count:000}",
                        PostingStatus = "Posted",
                        FunctionalCurrencyCode = request.FunctionalCurrencyCode,
                        PostingDate = request.PostingDate,
                        SourceDocumentId = request.SourceDocumentId,
                        SourceDocumentType = request.SourceDocumentType,
                        PostingAction = request.PostingAction
                    });
                });

            // Finance's currency master, as HrCurrencyBridge reads it: GHS base, USD at 12.5.
            Currencies.Setup(c => c.GetBaseCurrencyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new CurrencyDto { CurrencyCode = "GHS" });
            Currencies.Setup(c => c.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string code, CancellationToken _) => code is "GHS" or "USD" ? new CurrencyDto { CurrencyCode = code } : null);
            Currencies.Setup(c => c.ConvertAsync(1m, "USD", "GHS", It.IsAny<CancellationToken>())).ReturnsAsync(12.5m);
            Rates.Setup(r => r.GetCurrentRateAsync("USD", "GHS", It.IsAny<DateTime?>(), null, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ExchangeRateDto { Rate = 12.5m, BaseCurrencyCode = "GHS", TargetCurrencyCode = "USD" });
        }

        public HrFinancePostingAdapter Build() => new(
            Store.Object,
            UnitOfWork.Object,
            Engine.Object,
            new HrCurrencyBridge(Currencies.Object, Rates.Object),
            CurrentUser.Object,
            NullLogger<HrFinancePostingAdapter>.Instance);

        public HrFinancePostingRecord LastRecord => Updated.LastOrDefault() ?? Added.Last();
    }

    private static MedicalExpenseClaim MedicalClaim(decimal approved = 850m, ClaimStatus status = ClaimStatus.Approved) => new()
    {
        Id = new Guid("bbbbbbbb-0000-0000-0000-000000000001"),
        TenantId = Tenant,
        ClaimNumber = "MC-2026-00042",
        EmployeeId = EmployeeId,
        ExpenseType = default,
        ClaimDate = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc),
        AmountRequested = 900m,
        AmountApproved = approved,
        Status = status
    };

    private static StaffTravelExpenseClaim TravelClaim(decimal approved, decimal advanceDeducted, TravelPaymentMethod method) => new()
    {
        Id = new Guid("cccccccc-0000-0000-0000-000000000001"),
        TenantId = Tenant,
        ClaimNumber = "EXP-2026-00007",
        EmployeeId = EmployeeId,
        CurrencyCode = "GHS",
        Status = TravelClaimStatus.Paid,
        TotalApproved = approved,
        AdvanceDeducted = advanceDeducted,
        NetPayable = approved - advanceDeducted,
        PaymentMethod = method,
        PaymentReference = "PV-1001",
        PaidAt = new DateTime(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc)
    };

    private static StaffTravelAdvance Advance(decimal amount, string currency) => new()
    {
        Id = new Guid("dddddddd-0000-0000-0000-000000000001"),
        TenantId = Tenant,
        AdvanceNumber = "ADV-2026-00003",
        EmployeeId = EmployeeId,
        CurrencyCode = currency,
        ApprovedAmount = amount,
        RequestedAmount = amount,
        Status = TravelAdvanceStatus.Disbursed,
        DisbursedAt = new DateTime(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc)
    };

    // ── Happy path & contract ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MedicalClaimApproved_PostsABalancedRequestThatSatisfiesTheFinanceContract()
    {
        var h = new Harness();
        var claim = MedicalClaim();

        var outcome = await h.Build().PostAsync(HrFinancePostingCommandFactory.MedicalClaimApproved(claim), User);

        h.Sent.Should().ContainSingle();
        var request = h.Sent[0];
        request.ShouldSatisfyPostingContract(
            expectedOriginModule: "HR",
            expectedDocumentType: HrFinancePostingEventCatalog.SourceMedicalExpenseClaim,
            expectedDocumentId: claim.Id,
            expectedTenantId: Tenant);
        request.SourceModule.Should().Be("HR");
        request.AccountingBookCode.Should().Be("IFRS");
        request.PostingAction.Should().Be("Approve");
        request.SourceDocumentReference.Should().Be("MC-2026-00042");
        request.IdempotencyKey.Should().Be($"HR|MEDICAL_CLAIM_APPROVED|{claim.Id:N}|IFRS|POST|G1");
        request.Lines.Should().HaveCount(2);
        request.Lines.Should().ContainSingle(l => l.AccountId == MedicalExpenseAccount && l.DebitAmount == 850m && l.CreditAmount == 0m);
        request.Lines.Should().ContainSingle(l => l.AccountId == PayableAccount && l.CreditAmount == 850m && l.DebitAmount == 0m);
        request.Lines.Should().OnlyContain(l => l.TransactionCurrency == "GHS" && l.Dimensions.Count == 0 && l.FinanceDimensionSetId == null);

        outcome.Status.Should().Be(HrFinancePostingStatus.Posted);
        outcome.JournalEntryNumber.Should().Be("JE-HR-001");
        var record = h.LastRecord;
        record.Status.Should().Be(HrFinancePostingStatus.Posted);
        record.PostingEventId.Should().NotBeNull();
        record.JournalEntryId.Should().NotBeNull();
        record.Amount.Should().Be(850m);
        record.CurrencyCode.Should().Be("GHS");
        record.LinesSnapshot.Should().Contain("000-2120-0000");
        h.Begins.Should().Be(1);
        h.Commits.Should().Be(1);
        h.Rollbacks.Should().Be(0);
    }

    [Fact]
    public async Task RunAsync_CommitsTheSourceMutationAndThePostingTogether()
    {
        var h = new Harness();
        var claim = MedicalClaim(status: ClaimStatus.Pending);
        var mutated = false;

        var outcome = await h.Build().RunAsync(_ =>
        {
            claim.Status = ClaimStatus.Approved;
            mutated = true;
            return Task.FromResult<HrFinancePostingCommand?>(HrFinancePostingCommandFactory.MedicalClaimApproved(claim));
        }, User);

        mutated.Should().BeTrue();
        outcome!.Status.Should().Be(HrFinancePostingStatus.Posted);
        h.Sent.Should().ContainSingle();
        h.Commits.Should().Be(1);
    }

    [Fact]
    public async Task RunAsync_WithNoCommand_SavesAndCommitsWithoutTouchingFinance()
    {
        var h = new Harness();

        var outcome = await h.Build().RunAsync(_ => Task.FromResult<HrFinancePostingCommand?>(null), User);

        outcome.Should().BeNull();
        h.Sent.Should().BeEmpty();
        h.Added.Should().BeEmpty();
        h.Saves.Should().Be(1);
        h.Commits.Should().Be(1);
    }

    // ── Idempotency ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task IdenticalRetry_ReturnsTheOriginalPostingWithoutASecondFinanceCall()
    {
        var h = new Harness();
        var claim = MedicalClaim();
        var postedEvent = Guid.NewGuid();
        var postedJournal = Guid.NewGuid();
        h.Existing = new HrFinancePostingRecord
        {
            TenantId = Tenant,
            EventCode = HrFinancePostingEventCatalog.MedicalClaimApproved,
            SourceDocumentType = HrFinancePostingEventCatalog.SourceMedicalExpenseClaim,
            SourceDocumentId = claim.Id,
            Status = HrFinancePostingStatus.Posted,
            PostingEventId = postedEvent,
            JournalEntryId = postedJournal,
            JournalEntryNumber = "JE-HR-777",
            PostingAction = "Approve",
            IdempotencyKey = "k"
        };

        var outcome = await h.Build().PostAsync(HrFinancePostingCommandFactory.MedicalClaimApproved(claim), User);

        outcome.WasDuplicate.Should().BeTrue();
        outcome.PostingEventId.Should().Be(postedEvent);
        outcome.JournalEntryId.Should().Be(postedJournal);
        outcome.JournalEntryNumber.Should().Be("JE-HR-777");
        h.Sent.Should().BeEmpty("the register already knows the answer; Finance is not asked twice");
        h.Updated.Should().BeEmpty();
    }

    [Fact]
    public async Task IdempotencyKey_IsStableForTheSameSourceAndDiffersAcrossEventsAndBooks()
    {
        var id = Guid.NewGuid();
        HrFinancePostingAdapter.BuildIdempotencyKey("MEDICAL_CLAIM_APPROVED", id, "ifrs", 1)
            .Should().Be(HrFinancePostingAdapter.BuildIdempotencyKey("MEDICAL_CLAIM_APPROVED", id, "IFRS", 1));
        HrFinancePostingAdapter.BuildIdempotencyKey("MEDICAL_CLAIM_APPROVED", id, "IFRS", 1)
            .Should().NotBe(HrFinancePostingAdapter.BuildIdempotencyKey("MEDICAL_CLAIM_PAID", id, "IFRS", 1));
        HrFinancePostingAdapter.BuildIdempotencyKey("MEDICAL_CLAIM_APPROVED", id, "IFRS", 1)
            .Should().NotBe(HrFinancePostingAdapter.BuildIdempotencyKey("MEDICAL_CLAIM_APPROVED", id, "LOCAL_STATUTORY", 1));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ApprovalAndPaymentOfOneClaim_CarryDifferentPostingActions()
    {
        // Finance de-duplicates on (source type, source id, action); two HR events on one claim
        // must therefore never share an action, or the payment would come back as a duplicate of
        // the approval.
        var h = new Harness();
        var claim = MedicalClaim();
        claim.Status = ClaimStatus.Paid;
        claim.PaymentProcessed = true;
        claim.PaymentMethod = PaymentMethod.BankTransfer;

        await h.Build().PostAsync(HrFinancePostingCommandFactory.MedicalClaimApproved(claim), User);
        await h.Build().PostAsync(HrFinancePostingCommandFactory.MedicalClaimPaid(claim), User);

        h.Sent.Should().HaveCount(2);
        h.Sent.Select(r => r.PostingAction).Should().BeEquivalentTo(new[] { "Approve", "Pay" });
        h.Sent.Select(r => r.SourceDocumentType).Distinct().Should().ContainSingle();
        h.Sent[1].Lines.Should().ContainSingle(l => l.AccountId == PayableAccount && l.DebitAmount == 850m);
        h.Sent[1].Lines.Should().ContainSingle(l => l.AccountId == ClearingAccount && l.CreditAmount == 850m);
    }

    [Fact]
    public async Task RepostingAReversedRow_UsesTheNextGenerationSoFinanceCannotHandBackTheReversedOriginal()
    {
        var h = new Harness();
        var claim = MedicalClaim();
        h.Existing = new HrFinancePostingRecord
        {
            TenantId = Tenant,
            EventCode = HrFinancePostingEventCatalog.MedicalClaimApproved,
            SourceDocumentType = HrFinancePostingEventCatalog.SourceMedicalExpenseClaim,
            SourceDocumentId = claim.Id,
            Status = HrFinancePostingStatus.Reversed,
            Generation = 1,
            PostingAction = "Approve",
            IdempotencyKey = "old",
            ReversedAt = DateTime.UtcNow,
            ReversalReason = "wrong amount"
        };

        var outcome = await h.Build().PostAsync(HrFinancePostingCommandFactory.MedicalClaimApproved(claim), User);

        outcome.Status.Should().Be(HrFinancePostingStatus.Posted);
        h.Sent.Should().ContainSingle();
        h.Sent[0].PostingAction.Should().Be("Approve#2");
        h.Sent[0].IdempotencyKey.Should().EndWith("|G2");
        var record = h.LastRecord;
        record.Generation.Should().Be(2);
        record.ReversedAt.Should().BeNull();
        record.ReversalReason.Should().BeNull();
    }

    // ── Failure semantics ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task FinanceRefusal_RollsBackEverything_DoesNotMarkPosted_AndRecordsTheFailureForRetry()
    {
        var h = new Harness();
        h.EngineFailure = new InvalidOperationException("Posting period is not open.");
        var claim = MedicalClaim(status: ClaimStatus.Pending);

        var act = () => h.Build().RunAsync(_ =>
        {
            claim.Status = ClaimStatus.Approved;
            return Task.FromResult<HrFinancePostingCommand?>(HrFinancePostingCommandFactory.MedicalClaimApproved(claim));
        }, User);

        var ex = await act.Should().ThrowAsync<HrFinancePostingException>();
        ex.Which.Message.Should().Contain("Posting period is not open");
        ex.Which.Message.Should().Contain("MC-2026-00042");
        h.Rollbacks.Should().Be(1);
        h.Commits.Should().Be(0);
        h.Clears.Should().BeGreaterThan(0, "the rolled-back graph must not be re-persisted by the failure log");

        // The failure row is written AFTER the rollback, on a clean tracker.
        h.Added.Should().ContainSingle();
        var failed = h.Added[0];
        failed.Status.Should().Be(HrFinancePostingStatus.Failed);
        failed.PostingEventId.Should().BeNull();
        failed.JournalEntryId.Should().BeNull();
        failed.StatusReason.Should().Contain("Posting period is not open");
        failed.AttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task AFailedRow_NeverDemotesAPostedOne()
    {
        var h = new Harness();
        h.EngineFailure = new InvalidOperationException("Module locked.");
        var claim = MedicalClaim();
        // The register already holds a Posted row for this event (say, found only after rollback).
        var posted = new HrFinancePostingRecord
        {
            TenantId = Tenant, EventCode = HrFinancePostingEventCatalog.MedicalClaimApproved,
            SourceDocumentType = HrFinancePostingEventCatalog.SourceMedicalExpenseClaim,
            SourceDocumentId = claim.Id, Status = HrFinancePostingStatus.Failed, PostingAction = "Approve", IdempotencyKey = "k"
        };
        var calls = 0;
        h.Store.Setup(s => s.FindRecordAsync(Tenant, It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                calls++;
                // First lookup (inside the transaction): a retryable Failed row. Second lookup
                // (failure log, after rollback): somebody else posted it meanwhile.
                if (calls >= 2) posted.Status = HrFinancePostingStatus.Posted;
                return posted;
            });

        await FluentActions.Awaiting(() => h.Build().PostAsync(HrFinancePostingCommandFactory.MedicalClaimApproved(claim), User))
            .Should().ThrowAsync<HrFinancePostingException>();

        h.Updated.Should().BeEmpty("a Posted row is never rewritten as Failed");
        posted.Status.Should().Be(HrFinancePostingStatus.Posted);
    }

    // ── Configuration ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DisabledRule_LetsTheHrActionProceedAndLogsTheEventUnposted()
    {
        var h = new Harness();
        h.Rule = new HrFinancePostingRule { IsEnabled = false };
        var claim = MedicalClaim();

        var outcome = await h.Build().PostAsync(HrFinancePostingCommandFactory.MedicalClaimApproved(claim), User);

        outcome.Status.Should().Be(HrFinancePostingStatus.Unposted);
        outcome.StatusReason.Should().Contain("disabled");
        h.Sent.Should().BeEmpty();
        h.Commits.Should().Be(1);
        h.LastRecord.Amount.Should().Be(850m, "the queue must show what was NOT posted");
        h.LastRecord.IdempotencyKey.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task MissingRule_LetsTheHrActionProceedAndLogsTheEventUnposted()
    {
        var h = new Harness();
        h.Rule = null;

        var outcome = await h.Build().PostAsync(HrFinancePostingCommandFactory.MedicalClaimApproved(MedicalClaim()), User);

        outcome.Status.Should().Be(HrFinancePostingStatus.Unposted);
        outcome.StatusReason.Should().Contain("No posting rule exists");
        h.Sent.Should().BeEmpty();
        h.Commits.Should().Be(1);
    }

    [Fact]
    public async Task UnmappedRole_RefusesTheHrActionWhileTheRuleIsEnabled()
    {
        var h = new Harness();
        h.Mappings.Remove(HrFinanceAccountRole.MedicalExpense);

        var act = () => h.Build().PostAsync(HrFinancePostingCommandFactory.MedicalClaimApproved(MedicalClaim()), User);

        var ex = await act.Should().ThrowAsync<HrFinancePostingException>();
        ex.Which.Message.Should().Contain("No Finance account is mapped for Medical expense");
        h.Sent.Should().BeEmpty();
        h.Rollbacks.Should().Be(1);
        h.Added.Should().ContainSingle(r => r.Status == HrFinancePostingStatus.Failed);
    }

    [Fact]
    public async Task AccountOfTheWrongType_Refuses()
    {
        var h = new Harness();
        // The payable role pointed at an expense account.
        h.Mappings[HrFinanceAccountRole.StaffClaimsPayable] = MedicalExpenseAccount;

        var act = () => h.Build().PostAsync(HrFinancePostingCommandFactory.MedicalClaimApproved(MedicalClaim()), User);

        var ex = await act.Should().ThrowAsync<HrFinancePostingException>();
        ex.Which.Message.Should().Contain("needs a Liability account");
        h.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task InactiveAccount_Refuses()
    {
        var h = new Harness();
        h.Accounts[PayableAccount] = h.Accounts[PayableAccount] with { IsActive = false };

        var act = () => h.Build().PostAsync(HrFinancePostingCommandFactory.MedicalClaimApproved(MedicalClaim()), User);

        var ex = await act.Should().ThrowAsync<HrFinancePostingException>();
        ex.Which.Message.Should().Contain("inactive in Finance");
        h.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task AccountMissingOrCrossTenant_Refuses()
    {
        var h = new Harness();
        // The store answers only for the caller's tenant; an account from another tenant is simply absent.
        h.Accounts.Remove(PayableAccount);

        var act = () => h.Build().PostAsync(HrFinancePostingCommandFactory.MedicalClaimApproved(MedicalClaim()), User);

        var ex = await act.Should().ThrowAsync<HrFinancePostingException>();
        ex.Which.Message.Should().Contain("no longer exists in this organisation's chart");
        h.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task UnresolvedAccountingBook_Refuses()
    {
        var h = new Harness();
        h.Context = new HrFinanceTenantContext("GHS", null, "A multi-book selector cannot be submitted to the single-book posting executor.");

        var act = () => h.Build().PostAsync(HrFinancePostingCommandFactory.MedicalClaimApproved(MedicalClaim()), User);

        var ex = await act.Should().ThrowAsync<HrFinancePostingException>();
        ex.Which.Message.Should().Contain("accounting book is not configured");
        h.Sent.Should().BeEmpty();
    }

    // ── Travel: settlement legs, payroll route, foreign currency ─────────────────────────────

    [Fact]
    public async Task TravelClaimPaid_ClearsThePayableAgainstTheAdvanceAndTheClearingAccount()
    {
        var h = new Harness();
        var claim = TravelClaim(approved: 1000m, advanceDeducted: 300m, TravelPaymentMethod.BankTransfer);

        await h.Build().PostAsync(HrFinancePostingCommandFactory.TravelClaimPaid(claim), User);

        var request = h.Sent.Should().ContainSingle().Subject;
        request.ShouldSatisfyPostingContract("HR", HrFinancePostingEventCatalog.SourceStaffTravelExpenseClaim, claim.Id, Tenant);
        request.PostingAction.Should().Be("Pay");
        request.Lines.Should().HaveCount(3);
        request.Lines.Should().ContainSingle(l => l.AccountId == PayableAccount && l.DebitAmount == 1000m);
        request.Lines.Should().ContainSingle(l => l.AccountId == ReceivableAccount && l.CreditAmount == 300m);
        request.Lines.Should().ContainSingle(l => l.AccountId == ClearingAccount && l.CreditAmount == 700m);
    }

    [Fact]
    public async Task TravelClaimPaidByPayrollOffset_PostsOnlyTheAdvanceRecovery_OrSkipsWhenThereIsNone()
    {
        var h = new Harness();

        await h.Build().PostAsync(HrFinancePostingCommandFactory.TravelClaimPaid(
            TravelClaim(approved: 1000m, advanceDeducted: 300m, TravelPaymentMethod.PayrollOffset)), User);
        var withAdvance = h.Sent.Should().ContainSingle().Subject;
        withAdvance.Lines.Should().HaveCount(2);
        withAdvance.Lines.Should().ContainSingle(l => l.AccountId == PayableAccount && l.DebitAmount == 300m);
        withAdvance.Lines.Should().ContainSingle(l => l.AccountId == ReceivableAccount && l.CreditAmount == 300m);
        withAdvance.Lines.Should().NotContain(l => l.AccountId == ClearingAccount, "payroll's own journal clears the rest");

        h.Existing = null;
        var noAdvance = TravelClaim(approved: 1000m, advanceDeducted: 0m, TravelPaymentMethod.PayrollOffset);
        noAdvance.Id = Guid.NewGuid();
        var outcome = await h.Build().PostAsync(HrFinancePostingCommandFactory.TravelClaimPaid(noAdvance), User);
        outcome.Status.Should().Be(HrFinancePostingStatus.Skipped);
        outcome.StatusReason.Should().Contain("payroll");
        h.Sent.Should().HaveCount(1, "a skip never reaches Finance");
    }

    [Fact]
    public async Task ForeignCurrencyAdvance_IsValuedThroughFinanceRateAndKeepsTheOriginalAsEvidence()
    {
        var h = new Harness();
        var advance = Advance(100m, "USD");

        var outcome = await h.Build().PostAsync(HrFinancePostingCommandFactory.TravelAdvanceDisbursed(advance), User);

        outcome.Status.Should().Be(HrFinancePostingStatus.Posted);
        var request = h.Sent.Should().ContainSingle().Subject;
        request.ShouldSatisfyPostingContract("HR", HrFinancePostingEventCatalog.SourceStaffTravelAdvance, advance.Id, Tenant);
        request.PostingAction.Should().Be("Disburse");
        request.FunctionalCurrencyCode.Should().Be("GHS");
        request.Lines.Should().OnlyContain(l => l.TransactionCurrency == "GHS", "HR posts functional lines; the original is evidence, not a Finance FX leg");
        request.Lines.Should().ContainSingle(l => l.AccountId == ReceivableAccount && l.DebitAmount == 1250m);
        request.Lines.Should().ContainSingle(l => l.AccountId == ClearingAccount && l.CreditAmount == 1250m);
        request.Lines.Should().OnlyContain(l => l.Description!.Contains("USD 100.00 @ 12.5"));

        var record = h.LastRecord;
        record.Amount.Should().Be(1250m);
        record.CurrencyCode.Should().Be("GHS");
        record.TransactionCurrencyCode.Should().Be("USD");
        record.TransactionAmount.Should().Be(100m);
    }

    [Fact]
    public async Task ZeroAmount_IsSkippedWithoutTouchingFinance()
    {
        var h = new Harness();

        var outcome = await h.Build().PostAsync(HrFinancePostingCommandFactory.MedicalClaimApproved(MedicalClaim(approved: 0m)), User);

        outcome.Status.Should().Be(HrFinancePostingStatus.Skipped);
        h.Sent.Should().BeEmpty();
        h.Commits.Should().Be(1);
    }

    // ── Source guard ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EnsureNotPosted_RefusesNamingTheJournal_AndPassesWhenNothingIsPosted()
    {
        var h = new Harness();
        var claim = MedicalClaim();
        var adapter = h.Build();

        await adapter.Invoking(a => a.EnsureNotPostedAsync(HrFinancePostingEventCatalog.SourceMedicalExpenseClaim, claim.Id, "Editing this claim"))
            .Should().NotThrowAsync();

        h.Existing = new HrFinancePostingRecord
        {
            TenantId = Tenant, EventCode = HrFinancePostingEventCatalog.MedicalClaimApproved,
            SourceDocumentType = HrFinancePostingEventCatalog.SourceMedicalExpenseClaim, SourceDocumentId = claim.Id,
            SourceReference = "MC-2026-00042", Status = HrFinancePostingStatus.Posted, JournalEntryNumber = "JE-HR-009",
            PostingAction = "Approve", IdempotencyKey = "k"
        };
        var ex = await adapter.Invoking(a => a.EnsureNotPostedAsync(HrFinancePostingEventCatalog.SourceMedicalExpenseClaim, claim.Id, "Editing this claim"))
            .Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain("Editing this claim is not allowed");
        ex.Which.Message.Should().Contain("JE-HR-009");
        ex.Which.Message.Should().Contain("Reverse the posting");
    }
}
