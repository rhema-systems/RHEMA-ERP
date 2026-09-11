using System.Data;
using System.Data.Common;
using System.Text.Json;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Core.Services.Sales;
using ErpSystem.Core.Services.Workflow;
using ErpSystem.Data;
using ErpSystem.Data.Repositories;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

/// <summary>
/// Relational owner proof for the Sales C7/C11/C12/C13/C15 handoff. The named shared-memory
/// SQLite harness executes the production AccountingEventService and FinancePostingEngine.
/// It is provider evidence only: SQL Server schema, rowversion, and application-lock parity are
/// separately guarded and are not claimed by this fixture.
/// </summary>
public sealed class ArCreditNoteProducerRelationalFixtureTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-ARCreditNotePosting")]
    public async Task IndependentScopes_UseDurableIdOnlyC11Handoff_ThenCommitAndReplayReadOnly()
    {
        await using var fixture = await Fixture.CreateAsync();

        Guid eventId;
        await using (var maker = await fixture.CreateScopeAsync(fixture.MakerId))
        {
            var prepared = await maker.Service.PostCreditNoteAsync(fixture.CreditNote.Id);
            prepared.JournalEntryId.Should().BeNull("the maker only prepares C7 intent");
            eventId = await maker.Db.AccountingEvents.Select(x => x.Id).SingleAsync();
        }

        // This deliberately builds a new connection, DbContext and Finance/Sales service graph.
        // The checker receives the durable C11 identity only, never a request, selection or book.
        await using (var checker = await fixture.CreateScopeAsync(fixture.CheckerId))
        {
            await checker.Producer.ApprovePreparedAsync(eventId,
                new DecideProducerAccountingIntentDto { Reason = "Independent scope checker approval" });
            var approved = await checker.Db.AccountingEvents.SingleAsync(x => x.Id == eventId);
            approved.ProducerDecisionStatus.Should().Be(ProducerIntentDecisionStatuses.Approved);
            approved.ProducerDecidedByUserId.Should().Be(fixture.CheckerId);
        }

        Guid journalId;
        await using (var owner = await fixture.CreateScopeAsync(fixture.MakerId))
        {
            await using var transaction = await owner.Db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var posted = await owner.Service.PostCreditNoteAsync(fixture.CreditNote.Id);
            owner.Db.Database.CurrentTransaction.Should().NotBeNull("C12 execution joins the owner-owned transaction");
            await transaction.CommitAsync();
            journalId = posted.JournalEntryId!.Value;
        }

        await using (var retry = await fixture.CreateScopeAsync(fixture.MakerId))
        {
            var rowsBeforeRetry = await MappedRowsAsync(retry.Db);
            var replay = await retry.Service.PostCreditNoteAsync(fixture.CreditNote.Id);
            replay.JournalEntryId.Should().Be(journalId);
            (await MappedRowsAsync(retry.Db)).Should().Equal(rowsBeforeRetry,
                "C15 exact retry reloads the durable receipt/event through an independent context and is read-only");
        }
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARCreditNotePosting")]
    public async Task PrepareApproveAndPost_UsesRealRelationalProducerEvidence_AndCallerTransaction()
    {
        await using var fixture = await Fixture.CreateAsync();
        var prepared = await fixture.Service.PostCreditNoteAsync(fixture.CreditNote.Id);

        prepared.JournalEntryId.Should().BeNull();
        var pending = await fixture.Db.AccountingEvents.SingleAsync();
        pending.Status.Should().Be(AccountingEventStatuses.PendingApproval);
        pending.ProducerDecisionStatus.Should().Be(ProducerIntentDecisionStatuses.Pending);
        pending.ProducerIntentSnapshotJson.Should().NotBeNullOrWhiteSpace();
        (await fixture.Db.CreditNotes.SingleAsync()).JournalEntryId.Should().BeNull();

        fixture.UseChecker();
        const string checkerReason = "Independent Sales checker approval";
        await fixture.Producer.ApprovePreparedAsync(pending.Id,
            new DecideProducerAccountingIntentDto { Reason = checkerReason });
        fixture.Db.ChangeTracker.Clear();
        var approved = await fixture.Db.AccountingEvents.SingleAsync(x => x.Id == pending.Id);
        approved.ProducerDecisionStatus.Should().Be(ProducerIntentDecisionStatuses.Approved);
        approved.ProducerDecidedByUserId.Should().Be(fixture.CheckerId);
        approved.ProducerDecisionReason.Should().Be(checkerReason);
        approved.ProducerDecidedAtUtc.Should().NotBeNull();
        fixture.UseMaker();

        await using var callerTransaction = await fixture.Db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        CreditNoteDetailDto posted;
        try { posted = await fixture.Service.PostCreditNoteAsync(fixture.CreditNote.Id); }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new InvalidOperationException($"{fixture.Sql.LastDml}\n{fixture.State.LastFailureState}", ex);
        }
        fixture.Db.Database.CurrentTransaction.Should().NotBeNull("the owner joined the caller-owned transaction");
        await callerTransaction.CommitAsync();

        posted.JournalEntryId.Should().NotBeNull();
        fixture.Db.ChangeTracker.Entries().Should().OnlyContain(x => x.State == EntityState.Unchanged,
            "the committed owner transaction must leave no pending tracked mutation before reload");
        fixture.Db.ChangeTracker.Clear();
        var durable = await fixture.Db.AccountingEvents.Include(x => x.Postings).Include(x => x.ProducerReceipt).SingleAsync();
        durable.Status.Should().Be(AccountingEventStatuses.Posted);
        durable.ProducerDecisionStatus.Should().Be(ProducerIntentDecisionStatuses.Approved);
        durable.RequestFingerprint.Should().NotBeNullOrWhiteSpace();
        durable.Postings.Should().ContainSingle(x => x.FinancePostingEventId.HasValue && x.JournalEntryId.HasValue);
        durable.ProducerReceipt.Should().NotBeNull();
        durable.ProducerReceipt!.AccountingEventId.Should().Be(durable.Id);
        durable.ProducerReceipt.ParticipantCode.Should().Be("SALES.CREDIT_NOTE.V1");
        durable.ProducerReceipt.OwnerEntityType.Should().Be("SALES_CREDIT_NOTE");
        durable.ProducerReceipt.OwnerEntityId.Should().Be(fixture.CreditNote.Id);
        durable.ProducerReceipt.OwnerAction.Should().Be("POST");
        durable.ProducerReceipt.EffectFingerprint.Should().NotBeNullOrWhiteSpace();
        durable.ProducerReceipt.RequestFingerprint.Should().Be(durable.RequestFingerprint);
        durable.ProducerReceipt.RecordedByUserId.Should().Be(fixture.MakerId);
        durable.ProducerReceipt.RecordedAtUtc.Should().BeAfter(DateTime.UnixEpoch);
        (await fixture.Db.CreditNotes.SingleAsync()).JournalEntryId.Should().Be(posted.JournalEntryId);
        var finance = await fixture.Db.FinancePostingEvents.SingleAsync(x => x.SourceDocumentId == fixture.CreditNote.Id);
        finance.JournalEntryId.Should().Be(posted.JournalEntryId);
        finance.RequestFingerprint.Should().NotBeNullOrWhiteSpace("the C10 compatibility leaf retains its own canonical posting fingerprint");
        (await fixture.Db.JournalEntries.CountAsync()).Should().Be(1);
        (await fixture.Db.AccountTransactions.CountAsync()).Should().Be(2);
        (await fixture.Db.AccountBalances.CountAsync()).Should().BeGreaterThan(0);
        (await fixture.Db.AuditLogs.CountAsync()).Should().BeGreaterThan(0, "success uses the real durable audit service");

        var retryBefore = await MappedRowsAsync(fixture.Db);
        var retry = await fixture.Service.PostCreditNoteAsync(fixture.CreditNote.Id);
        retry.JournalEntryId.Should().Be(posted.JournalEntryId);
        (await fixture.Db.FinancePostingEvents.CountAsync()).Should().Be(2, "an exact retry is Finance and owner read-only; the fixture also carries the original invoice authority");
        (await fixture.Db.AccountingEventProducerReceipts.CountAsync()).Should().Be(1, "C15 retry cannot duplicate the C12 receipt");
        (await MappedRowsAsync(fixture.Db)).Should().Equal(retryBefore, "C15 exact retry is read-only across every mapped fixture row, including binary columns");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARCreditNotePosting")]
    public async Task InjectedC6Failure_RollsBackOwnerAndFinanceEffects_PersistsOneFailure_ThenRepairs()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Service.PostCreditNoteAsync(fixture.CreditNote.Id);
        var eventId = (await fixture.Db.AccountingEvents.SingleAsync()).Id;
        fixture.UseChecker();
        await fixture.Producer.ApprovePreparedAsync(eventId, new DecideProducerAccountingIntentDto { Reason = "Independent checker" });
        fixture.UseMaker();
        var beforeFailure = await MappedRowsAsync(fixture.Db);
        // The production leaf accepts this deliberate pre-commit audit fault after it has
        // written its owner and Finance effects; the owner must roll both back before C12
        // records the separate durable failure evidence.
        fixture.Audit.FailPostingAudit = true;

        await FluentActions.Awaiting(() => fixture.Service.PostCreditNoteAsync(fixture.CreditNote.Id))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("injected production posting audit failure");

        fixture.Db.ChangeTracker.Entries().Should().OnlyContain(x => x.State == EntityState.Unchanged,
            "C12 failure evidence is recorded only after the caller-owned owner transaction rolled back");
        fixture.Db.ChangeTracker.Clear();
        (await fixture.Db.CreditNotes.SingleAsync()).JournalEntryId.Should().BeNull();
        (await fixture.Db.FinancePostingEvents.CountAsync()).Should().Be(1, "only the seeded invoice authority is durable after rollback");
        (await fixture.Db.JournalEntries.CountAsync()).Should().Be(0);
        var failed = await fixture.Db.AccountingEvents.Include(x => x.Attempts).SingleAsync();
        failed.Status.Should().Be(AccountingEventStatuses.Failed);
        failed.Attempts.Should().ContainSingle(x => x.Status == AccountingEventStatuses.Failed);
        fixture.Db.Database.CurrentTransaction.Should().BeNull();
        fixture.Db.ChangeTracker.Entries().Should().OnlyContain(x => x.State == EntityState.Unchanged);
        var afterFailure = await MappedRowsAsync(fixture.Db);
        afterFailure.Should().NotEqual(beforeFailure, "only the independently durable failed attempt is permitted after the owner transaction rolls back");

        fixture.Audit.FailPostingAudit = false;
        var repaired = await fixture.Service.PostCreditNoteAsync(fixture.CreditNote.Id);
        repaired.JournalEntryId.Should().NotBeNull();
        (await fixture.Db.AccountingEvents.Include(x => x.Attempts).SingleAsync()).Attempts
            .Count(x => x.Status == AccountingEventStatuses.Failed).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARCreditNotePosting")]
    public async Task ClosedBookPeriod_RejectsApprovedExecutionWithoutOwnerOrFinanceMutation_AndFreshOpenScenarioPosts()
    {
        await using (var closed = await Fixture.CreateAsync(postingPeriodOpen: false))
        {
            await closed.Service.PostCreditNoteAsync(closed.CreditNote.Id);
            var eventId = (await closed.Db.AccountingEvents.SingleAsync()).Id;
            closed.UseChecker();
            await closed.Producer.ApprovePreparedAsync(eventId, new DecideProducerAccountingIntentDto { Reason = "Independent checker approval" });
            closed.UseMaker();

            var selectionCount = await closed.Db.AccountingBookSelectionEvidence.CountAsync();
            var ownerReceiptCount = await closed.Db.AccountingEventProducerReceipts.CountAsync();
            var postingCount = await closed.Db.FinancePostingEvents.CountAsync();
            var journalCount = await closed.Db.JournalEntries.CountAsync();
            var balanceCount = await closed.Db.AccountBalances.CountAsync();
            var originalUpdatedAt = (await closed.Db.CreditNotes.SingleAsync()).UpdatedAt;
            var beforeRejection = await MappedRowsAsync(closed.Db);

            await FluentActions.Awaiting(() => closed.Service.PostCreditNoteAsync(closed.CreditNote.Id))
                .Should().ThrowAsync<InvalidOperationException>().WithMessage("*not open*");

            closed.Db.ChangeTracker.Entries().Should().OnlyContain(x => x.State == EntityState.Unchanged,
                "closed-period rejection must leave no pending owner or Finance mutation before reload");
            closed.Db.ChangeTracker.Clear();
            (await closed.Db.CreditNotes.SingleAsync()).JournalEntryId.Should().BeNull();
            (await closed.Db.CreditNotes.SingleAsync()).UpdatedAt.Should().Be(originalUpdatedAt);
            (await closed.Db.AccountingBookSelectionEvidence.CountAsync()).Should().Be(selectionCount, "C5 evidence is immutable through a failed approved execution");
            (await closed.Db.AccountingEventProducerReceipts.CountAsync()).Should().Be(ownerReceiptCount);
            (await closed.Db.FinancePostingEvents.CountAsync()).Should().Be(postingCount);
            (await closed.Db.JournalEntries.CountAsync()).Should().Be(journalCount);
            (await closed.Db.AccountBalances.CountAsync()).Should().Be(balanceCount);
            (await MappedRowsAsync(closed.Db)).Where(row => !IsFailureEvidenceRow(row)).Should().Equal(
                beforeRejection.Where(row => !IsFailureEvidenceRow(row)),
                "a C10 period gate may append only C12 failure event/attempt/audit evidence; owner, C5, posting, journal, balance and receipt rows remain frozen");
        }

        // Recovery is an independently prepared scenario; the rejected C11 authority is never
        // altered or reused after a closed-period decision.
        await using var reopenedScenario = await Fixture.CreateAsync(postingPeriodOpen: true);
        await reopenedScenario.Service.PostCreditNoteAsync(reopenedScenario.CreditNote.Id);
        var freshEventId = (await reopenedScenario.Db.AccountingEvents.SingleAsync()).Id;
        reopenedScenario.UseChecker();
        await reopenedScenario.Producer.ApprovePreparedAsync(freshEventId, new DecideProducerAccountingIntentDto { Reason = "Independent checker approval" });
        reopenedScenario.UseMaker();
        (await reopenedScenario.Service.PostCreditNoteAsync(reopenedScenario.CreditNote.Id)).JournalEntryId.Should().NotBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Batch", "FinanceGoLive-ARCreditNotePosting")]
    public async Task InvoiceScopedSerialization_PreventsOverCreditInEitherWinnerOrder(bool reverseWinnerOrder)
    {
        await using var fixture = await Fixture.CreateAsync(creditAmount: 60m);
        var second = await fixture.AddCreditNoteAsync(60m, "SCN-R2");
        var first = fixture.CreditNote;
        var ordered = reverseWinnerOrder ? new[] { second, first } : new[] { first, second };

        // Both independent C11 approvals exist before either owner acquires the invoice-scoped
        // Serializable lock. SQLite serializes writers, so execute deterministically in each
        // winner order and assert the second fresh reload observes the first durable credit.
        foreach (var credit in ordered)
            await fixture.Service.PostCreditNoteAsync(credit.Id);
        fixture.UseChecker();
        foreach (var eventId in await fixture.Db.AccountingEvents.OrderBy(x => x.CreatedAt).Select(x => x.Id).ToListAsync())
            await fixture.Producer.ApprovePreparedAsync(eventId, new DecideProducerAccountingIntentDto { Reason = "Independent checker approval" });
        fixture.UseMaker();

        (await fixture.Service.PostCreditNoteAsync(ordered[0].Id)).JournalEntryId.Should().NotBeNull();
        await FluentActions.Awaiting(() => fixture.Service.PostCreditNoteAsync(ordered[1].Id))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*exceed eligible credit amount*");

        fixture.Db.ChangeTracker.Clear();
        (await fixture.Db.FinancePostingEvents.CountAsync(x => x.SourceDocumentType == "SalesCreditNote" && x.PostingStatus == "Posted"))
            .Should().Be(1);
        (await fixture.Db.CreditNotes.SingleAsync(x => x.Id == ordered[1].Id)).JournalEntryId.Should().BeNull();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARCreditNotePosting")]
    public async Task C13Reversal_PreparesThenRequiresIndependentApproval_AndC15RetryIsReadOnly()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Service.PostCreditNoteAsync(fixture.CreditNote.Id);
        var originalId = (await fixture.Db.AccountingEvents.SingleAsync()).Id;
        fixture.UseChecker();
        await fixture.Producer.ApprovePreparedAsync(originalId, new DecideProducerAccountingIntentDto { Reason = "Independent original approval" });
        fixture.UseMaker();
        var posted = await fixture.Service.PostCreditNoteAsync(fixture.CreditNote.Id);
        await fixture.AlignFrozenC5AuthorityFromOriginalAsync(originalId);

        var request = new ReverseCreditNoteDto { Reason = "Customer return corrected", ReversalDate = new DateTime(2026, 9, 2) };
        var prepared = await fixture.Service.ReverseCreditNoteAsync(fixture.CreditNote.Id, request);
        prepared.CreditNoteStatus.Should().NotBe(CreditNoteStatus.Reversed, "C13 remains pending until a distinct checker decides it");
        var reversal = await fixture.Db.AccountingEvents.SingleAsync(x => x.Id != originalId);
        reversal.Status.Should().Be(AccountingEventStatuses.PendingApproval);
        reversal.ReversesAccountingEventId.Should().Be(originalId);
        reversal.ProducerDecisionStatus.Should().Be(ProducerIntentDecisionStatuses.Pending);

        fixture.UseChecker();
        await fixture.Producer.ApprovePreparedAsync(reversal.Id, new DecideProducerAccountingIntentDto { Reason = "Independent reversal approval" });
        fixture.UseMaker();
        var reversed = await fixture.Service.ReverseCreditNoteAsync(fixture.CreditNote.Id, request);
        reversed.CreditNoteStatus.Should().Be(CreditNoteStatus.Reversed);
        reversed.ReversalJournalEntryId.Should().NotBeNull();
        reversed.ReversalPostingEventId.Should().NotBeNull();
        var frozenRows = await MappedRowsAsync(fixture.Db);

        var replay = await fixture.Service.ReverseCreditNoteAsync(fixture.CreditNote.Id, request);
        replay.ReversalJournalEntryId.Should().Be(reversed.ReversalJournalEntryId);
        replay.ReversalPostingEventId.Should().Be(reversed.ReversalPostingEventId);
        (await MappedRowsAsync(fixture.Db)).Should().Equal(frozenRows,
            "C15 reversal retry must replay only the frozen C13 identity and never create a second owner or Finance effect");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly string _connectionString;
        private readonly Mock<ICurrentUserService> _currentUser;
        private readonly Guid _maker;
        private readonly Guid _checker;
        private readonly Guid _tenant;
        public ApplicationDbContext Db { get; }
        public CreditNote CreditNote { get; }
        public ReturnOrderService Service { get; }
        public FinanceProducerIntentService Producer { get; }
        public FaultingAudit Audit { get; }
        public SqlCapture Sql { get; }
        public StateCapture State { get; }
        public Guid MakerId => _maker;
        public Guid CheckerId => _checker;

        private Fixture(SqliteConnection connection, ApplicationDbContext db, CreditNote creditNote,
            ReturnOrderService service, FinanceProducerIntentService producer, FaultingAudit audit, SqlCapture sql, StateCapture state,
            Mock<ICurrentUserService> currentUser, Guid tenant, Guid maker, Guid checker)
        { _connection = connection; _connectionString = connection.ConnectionString; Db = db; CreditNote = creditNote; Service = service; Producer = producer; Audit = audit; Sql = sql; State = state; _currentUser = currentUser; _tenant = tenant; _maker = maker; _checker = checker; }

        public static async Task<Fixture> CreateAsync(bool postingPeriodOpen = true, decimal creditAmount = 100m)
        {
            // Named shared memory makes each role scope capable of opening a separate relational
            // connection while the fixture's keeper connection preserves the database lifetime.
            var connection = new SqliteConnection($"Data Source=ar-credit-note-{Guid.NewGuid():N};Mode=Memory;Cache=Shared"); await connection.OpenAsync();
            var sql = new SqlCapture();
            var state = new StateCapture();
            var db = new SalesSqliteDbContext(new DbContextOptionsBuilder<SalesSqliteDbContext>().UseSqlite(connection).AddInterceptors(sql, state).Options);
            await CreateSchemaAsync(db);
            var tenant = Guid.NewGuid(); var maker = Guid.NewGuid(); var checker = Guid.NewGuid();
            var user = new Mock<ICurrentUserService>();
            user.SetupGet(x => x.TenantId).Returns(tenant); user.SetupGet(x => x.UserId).Returns(() => maker.ToString());
            user.SetupGet(x => x.UserName).Returns("sales-maker"); user.SetupGet(x => x.IsAuthenticated).Returns(true);
            var book = new AccountingBook { Id = Guid.NewGuid(), TenantId = tenant, Code = "IFRS", Name = "IFRS", IsDefault = true, IsActive = true, AllowsPosting = true, BookType = AccountingBookType.PrimaryFull, FunctionalCurrencyCode = "GHS" };
            var fiscalYear = new FiscalYear { Id = Guid.NewGuid(), TenantId = tenant, FiscalYearName = "FY 2026", FiscalYearCode = "2026", Year = 2026, StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31), TotalDays = 365, Status = "Open", IsActive = true };
            var fiscalPeriod = new FiscalPeriod { Id = Guid.NewGuid(), TenantId = tenant, FiscalYearId = fiscalYear.Id, PeriodName = "September 2026", PeriodCode = "2026-09", PeriodNumber = 9, StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2026, 9, 30), PeriodDays = 30, PeriodStatus = postingPeriodOpen ? "Open" : "Closed", IsOpen = postingPeriodOpen, IsClosed = !postingPeriodOpen };
            var bookPeriod = new AccountingBookPeriod { Id = Guid.NewGuid(), TenantId = tenant, AccountingBookId = book.Id, FiscalPeriodId = fiscalPeriod.Id, PeriodStatus = postingPeriodOpen ? AccountingBookPeriodStatus.Open : AccountingBookPeriodStatus.Closed };
            var ar = Account(tenant, "1200", AccountType.Asset, true, false); var returns = Account(tenant, "5200", AccountType.Expense, false, true);
            var arBook = new AccountAccountingBook { Id = Guid.NewGuid(), TenantId = tenant, AccountId = ar.Id, AccountingBookId = book.Id, IsEnabled = true };
            var returnsBook = new AccountAccountingBook { Id = Guid.NewGuid(), TenantId = tenant, AccountId = returns.Id, AccountingBookId = book.Id, IsEnabled = true };
            var partner = new BusinessPartner { Id = Guid.NewGuid(), TenantId = tenant, PartnerCode = "C1", PartnerName = "Relational customer", PartnerType = "Customer", IsActive = true, DefaultArAccountId = ar.Id };
            var invoiceJournalId = Guid.NewGuid();
            var invoice = new Invoice { Id = Guid.NewGuid(), TenantId = tenant, BusinessPartnerId = partner.Id, InvoiceNumber = "SI-R1", CustomerName = partner.PartnerName, InvoiceDate = new DateTime(2026, 9, 1), CurrencyCode = "GHS", ExchangeRate = 1, TotalAmount = 100m, BaseCurrencyAmount = 100m, JournalEntryId = invoiceJournalId, Status = InvoiceStatus.Approved };
            var invoicePosting = new FinancePostingEvent { Id = Guid.NewGuid(), TenantId = tenant, SourceModule = "AR", OriginModuleCode = "SALES", SourceDocumentType = "CustomerInvoice", SourceDocumentId = invoice.Id, PostingAction = "Post", PostingStatus = "Posted", PostingDate = invoice.InvoiceDate, PostedAt = DateTime.UtcNow, JournalEntryId = invoiceJournalId, AccountingBookId = book.Id, BookClassification = book.Code, FunctionalCurrencyCode = "GHS" };
            var credit = new CreditNote { Id = Guid.NewGuid(), TenantId = tenant, BusinessPartnerId = partner.Id, OriginalInvoiceId = invoice.Id, DocumentNumber = "SCN-R1", DocumentDate = new DateTime(2026, 9, 1), Currency = "GHS", ExchangeRate = 1, CreditNoteStatus = CreditNoteStatus.Approved, TotalAmount = creditAmount, CreatedAt = DateTime.UtcNow, CreatedBy = "seed" };
            credit.Lines.Add(new CreditNoteLine { Id = Guid.NewGuid(), TenantId = tenant, CreditNoteId = credit.Id, Description = "Return", Quantity = 1, UnitPrice = creditAmount, CreatedAt = DateTime.UtcNow, CreatedBy = "seed" });
            // C7 records its event identity at preparation time; the frozen C5 fixture must
            // carry that same date for the C13 original-authority reconstruction check.
            var selection = new AccountingBookSelectionEvidence { Id = Guid.NewGuid(), TenantId = tenant, EffectiveDate = DateTime.UtcNow.Date,
                OriginatingModuleCode = "SALES", SourceDocumentType = "SalesCreditNote", PostingAction = "Post", IdempotencyKey = $"AR-CN-{credit.Id:N}",
                CalculationInputHash = Hash('A'), SelectionFingerprint = Hash('B'), FrozenByUserId = maker, FrozenAtUtc = DateTime.UtcNow };
            selection.Books.Add(new AccountingBookSelectionEvidenceBook { Id = Guid.NewGuid(), TenantId = tenant, AccountingBookSelectionEvidenceId = selection.Id,
                AccountingBookId = book.Id, SelectionOrder = 1, AccountingBookCodeSnapshot = book.Code, AuthorityFingerprint = Hash('C') });
            db.AddRange(new Tenant { Id = tenant, Code = "REL", Name = "Relational fixture", BaseCurrency = "GHS" }, fiscalYear, fiscalPeriod, book, bookPeriod, ar, returns, arBook, returnsBook, partner, invoice, invoicePosting, credit, selection, new FinanceSettings { Id = Guid.NewGuid(), TenantId = tenant, BaseCurrency = "GHS", ControlAccountArId = ar.Id, DiscountAllowedAccountId = returns.Id });
            await db.SaveChangesAsync();
            var applicability = new Mock<IAccountingBookApplicabilityService>();
            var frozen = new AccountingBookSelectionDto { SelectionEvidenceId = selection.Id, EffectiveDate = selection.EffectiveDate, OriginatingModuleCode = "SALES", SourceDocumentType = "SalesCreditNote", PostingAction = "Post", CalculationInputHash = Hash('A'), SelectionFingerprint = Hash('B'), Books = [new AccountingBookSelectionBookDto { AccountingBookId = book.Id, AccountingBookCode = book.Code, SelectionOrder = 1, AuthorityFingerprint = Hash('C') }] };
            applicability.Setup(x => x.ResolveAsync(It.IsAny<ResolveAccountingBookApplicabilityDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(frozen);
            applicability.Setup(x => x.FreezeAsync(It.IsAny<FreezeAccountingBookSelectionDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(frozen);
            var durableAudit = new FinanceAuditService(db, user.Object, new HttpContextAccessor { HttpContext = new DefaultHttpContext() });
            var audit = new FaultingAudit(durableAudit);
            var events = new AccountingEventService(db, user.Object, applicability.Object,
                new FinancePostingEngine(db, user.Object, NullLogger<FinancePostingEngine>.Instance, audit), audit,
                Options.Create(new AccountingEventOptions { Enabled = true }));
            var producer = new FinanceProducerIntentService(applicability.Object, events, events, db, user.Object,
                Options.Create(new FinanceProducerIntentOptions { Enabled = true }));
            var provider = new Mock<ICurrentUserProvider>(); provider.SetupGet(x => x.TenantId).Returns(tenant); provider.SetupGet(x => x.UserId).Returns(maker); provider.SetupGet(x => x.Username).Returns("sales-maker");
            var workflow = new Mock<IWorkflowIntegrationService>();
            var service = new ReturnOrderService(new GenericRepository<ReturnOrder>(db), new GenericRepository<ReturnOrderLine>(db), new GenericRepository<CreditNote>(db), new GenericRepository<CreditNoteLine>(db), new GenericRepository<Refund>(db), new UnitOfWork(db), provider.Object, Mock.Of<IDocumentNumberingService>(), workflow.Object,
                new WorkflowStatusAdapterRegistry([new CreditNoteWorkflowStatusAdapter(), new RefundWorkflowStatusAdapter()]), NullLogger<ReturnOrderService>.Instance,
                financeAuditService: durableAudit, financeProducerIntents: producer, financeProducerExecution: producer, financeProducerReversals: producer, financeProducerReplayVerifier: producer);
            return new Fixture(connection, db, credit, service, producer, audit, sql, state, user, tenant, maker, checker);
        }

        public async Task<Scope> CreateScopeAsync(Guid userId)
        {
            var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            var db = new SalesSqliteDbContext(new DbContextOptionsBuilder<SalesSqliteDbContext>().UseSqlite(connection).Options);
            var user = new Mock<ICurrentUserService>();
            user.SetupGet(x => x.TenantId).Returns(_tenant);
            user.SetupGet(x => x.UserId).Returns(userId.ToString());
            user.SetupGet(x => x.UserName).Returns(userId == _checker ? "sales-checker" : "sales-maker");
            user.SetupGet(x => x.IsAuthenticated).Returns(true);
            var frozenEvidence = await db.AccountingBookSelectionEvidence
                .Include(x => x.Books).SingleAsync(x => x.TenantId == _tenant);
            var frozen = new AccountingBookSelectionDto
            {
                SelectionEvidenceId = frozenEvidence.Id,
                EffectiveDate = frozenEvidence.EffectiveDate,
                OriginatingModuleCode = frozenEvidence.OriginatingModuleCode,
                SourceDocumentType = frozenEvidence.SourceDocumentType,
                PostingAction = frozenEvidence.PostingAction,
                CalculationInputHash = frozenEvidence.CalculationInputHash,
                SelectionFingerprint = frozenEvidence.SelectionFingerprint,
                Books = frozenEvidence.Books.OrderBy(x => x.SelectionOrder).Select(x => new AccountingBookSelectionBookDto
                {
                    AccountingBookId = x.AccountingBookId,
                    AccountingBookCode = x.AccountingBookCodeSnapshot,
                    SelectionOrder = x.SelectionOrder,
                    AuthorityFingerprint = x.AuthorityFingerprint
                }).ToList()
            };
            var applicability = new Mock<IAccountingBookApplicabilityService>();
            applicability.Setup(x => x.ResolveAsync(It.IsAny<ResolveAccountingBookApplicabilityDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(frozen);
            applicability.Setup(x => x.FreezeAsync(It.IsAny<FreezeAccountingBookSelectionDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(frozen);
            var durableAudit = new FinanceAuditService(db, user.Object, new HttpContextAccessor { HttpContext = new DefaultHttpContext() });
            var audit = new FaultingAudit(durableAudit);
            var events = new AccountingEventService(db, user.Object, applicability.Object,
                new FinancePostingEngine(db, user.Object, NullLogger<FinancePostingEngine>.Instance, audit), audit,
                Options.Create(new AccountingEventOptions { Enabled = true }));
            var producer = new FinanceProducerIntentService(applicability.Object, events, events, db, user.Object,
                Options.Create(new FinanceProducerIntentOptions { Enabled = true }));
            var provider = new Mock<ICurrentUserProvider>();
            provider.SetupGet(x => x.TenantId).Returns(_tenant); provider.SetupGet(x => x.UserId).Returns(userId); provider.SetupGet(x => x.Username).Returns(userId == _checker ? "sales-checker" : "sales-maker");
            var service = new ReturnOrderService(new GenericRepository<ReturnOrder>(db), new GenericRepository<ReturnOrderLine>(db), new GenericRepository<CreditNote>(db), new GenericRepository<CreditNoteLine>(db), new GenericRepository<Refund>(db), new UnitOfWork(db), provider.Object, Mock.Of<IDocumentNumberingService>(), Mock.Of<IWorkflowIntegrationService>(),
                new WorkflowStatusAdapterRegistry([new CreditNoteWorkflowStatusAdapter(), new RefundWorkflowStatusAdapter()]), NullLogger<ReturnOrderService>.Instance,
                financeAuditService: durableAudit, financeProducerIntents: producer, financeProducerExecution: producer, financeProducerReversals: producer, financeProducerReplayVerifier: producer);
            return new Scope(connection, db, service, producer);
        }

        public void UseChecker() => _currentUser.SetupGet(x => x.UserId).Returns(_checker.ToString());
        public void UseMaker() => _currentUser.SetupGet(x => x.UserId).Returns(_maker.ToString());
        public async Task<CreditNote> AddCreditNoteAsync(decimal amount, string number)
        {
            var template = await Db.CreditNotes.Include(x => x.Lines).SingleAsync(x => x.Id == CreditNote.Id);
            var additional = new CreditNote { Id = Guid.NewGuid(), TenantId = template.TenantId, BusinessPartnerId = template.BusinessPartnerId, OriginalInvoiceId = template.OriginalInvoiceId, DocumentNumber = number, DocumentDate = template.DocumentDate, Currency = template.Currency, ExchangeRate = template.ExchangeRate, CreditNoteStatus = CreditNoteStatus.Approved, TotalAmount = amount, CreatedAt = DateTime.UtcNow, CreatedBy = "seed" };
            additional.Lines.Add(new CreditNoteLine { Id = Guid.NewGuid(), TenantId = additional.TenantId, CreditNoteId = additional.Id, Description = "Return", Quantity = 1, UnitPrice = amount, CreatedAt = DateTime.UtcNow, CreatedBy = "seed" });
            Db.Add(additional);
            await Db.SaveChangesAsync();
            return additional;
        }
        public async Task AlignFrozenC5AuthorityFromOriginalAsync(Guid eventId)
        {
            // The relational harness seeds C5 directly. Rehydrate its canonical hash values
            // from the durable C7 request rather than duplicating production fingerprint logic.
            var original = await Db.AccountingEvents.SingleAsync(x => x.Id == eventId);
            using var json = JsonDocument.Parse(original.ProducerIntentSnapshotJson!);
            var root = json.RootElement;
            var calculation = root.GetProperty("expectedCalculationInputHash").GetString()!;
            var fingerprint = root.GetProperty("expectedSelectionFingerprint").GetString()!;
            var evidence = await Db.AccountingBookSelectionEvidence.SingleAsync(x => x.Id == original.AccountingBookSelectionEvidenceId);
            evidence.CalculationInputHash = calculation;
            evidence.SelectionFingerprint = fingerprint;
            evidence.EffectiveDate = original.EventDate.Date;
            evidence.OriginatingModuleCode = original.OriginatingModuleCode;
            evidence.SourceDocumentType = original.SourceDocumentType;
            evidence.PostingAction = original.PostingAction;
            original.SelectionFingerprint = fingerprint;
            await Db.SaveChangesAsync();
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await _connection.DisposeAsync(); }
    }

    private sealed class Scope(SqliteConnection connection, ApplicationDbContext db, ReturnOrderService service,
        FinanceProducerIntentService producer) : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; } = db;
        public ReturnOrderService Service { get; } = service;
        public FinanceProducerIntentService Producer { get; } = producer;
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }

    private sealed class FaultingAudit(IFinanceAuditService inner) : IFinanceAuditService
    {
        public bool FailPostingAudit { get; set; }
        public Task<AuditLog> RecordAsync(FinanceAuditEventDto auditEvent, CancellationToken cancellationToken = default)
        {
            if (FailPostingAudit && auditEvent.EventType == FinanceAuditEvents.PostingEventCreated)
                throw new InvalidOperationException("injected production posting audit failure");
            return inner.RecordAsync(auditEvent, cancellationToken);
        }

        public Task<IReadOnlyList<AuditLog>> GetAuditTrailAsync(Guid tenantId, string resource,
            string resourceId, int limit = 100, CancellationToken cancellationToken = default) =>
            inner.GetAuditTrailAsync(tenantId, resource, resourceId, limit, cancellationToken);
    }

    private sealed class SqlCapture : DbCommandInterceptor
    {
        public string LastDml { get; private set; } = "No UPDATE/DELETE command captured.";
        private void Capture(DbCommand command)
        {
            if (!command.CommandText.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase) &&
                !command.CommandText.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase)) return;
            LastDml = command.CommandText + " | " + string.Join(", ", command.Parameters.Cast<DbParameter>().Select(parameter => parameter.ParameterName + "=" + Format(parameter.Value)));
        }
        private static string Format(object? value) => value switch { byte[] bytes => Convert.ToHexString(bytes), string text when text.Length > 128 => text[..128] + "...", _ => Convert.ToString(value) ?? "<null>" };
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result) { Capture(command); return result; }
        public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result) { Capture(command); return result; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) { Capture(command); return ValueTask.FromResult(result); }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) { Capture(command); return ValueTask.FromResult(result); }
    }

    private sealed class StateCapture : SaveChangesInterceptor
    {
        public string LastFailureState { get; private set; } = "No SaveChanges failure state captured.";
        public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
        {
            LastFailureState = eventData.Context is null ? "No DbContext." : string.Join("\n", eventData.Context.ChangeTracker.Entries().Where(entry => entry.State != EntityState.Unchanged).Select(Describe));
            return Task.CompletedTask;
        }
        private static string Describe(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry) =>
            $"{entry.Metadata.ClrType.Name} {entry.State}: " + string.Join(", ", entry.Properties.Where(property => property.Metadata.IsPrimaryKey() || property.IsModified).Select(property => $"{property.Metadata.Name} original={Format(property.OriginalValue)} current={Format(property.CurrentValue)}"));
        private static string Format(object? value) => value is byte[] bytes ? Convert.ToHexString(bytes) : Convert.ToString(value) ?? "<null>";
    }

    private sealed class SalesSqliteDbContext(DbContextOptions<SalesSqliteDbContext> options) : ApplicationDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // SQL Server supplies rowversion values. This test-local SQLite provider model
            // retains optimistic-concurrency predicates while making their tokens explicit.
            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                var rowVersion = entity.FindProperty("RowVersion");
                if (rowVersion is not null)
                {
                    rowVersion.ValueGenerated = ValueGenerated.Never;
                    rowVersion.IsConcurrencyToken = false;
                }
                foreach (var property in entity.GetProperties())
                {
                    // EF's SQLite provider rejects SUM(decimal), while the production SQL
                    // Server provider executes the authoritative invoice-credit aggregate.
                    // This local relational model stores decimals as doubles only so that the
                    // unmodified production LINQ query can run under the C14 provider harness.
                    if (property.ClrType == typeof(decimal) || property.ClrType == typeof(decimal?))
                        property.SetProviderClrType(typeof(double));
                }
            }
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // Query current transactional state, never an in-memory success cache: a row that
            // was inserted before rollback must be reinserted during C12 recovery.
            foreach (var entry in ChangeTracker.Entries<BaseEntity>().Where(entry => entry.State == EntityState.Modified))
                if (!await ExistsAsync(entry, cancellationToken)) entry.State = EntityState.Added;
            return await base.SaveChangesAsync(cancellationToken);
        }

        private async Task<bool> ExistsAsync(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<BaseEntity> entry, CancellationToken cancellationToken)
        {
            var store = StoreObjectIdentifier.Table(entry.Metadata.GetTableName()!, entry.Metadata.GetSchema());
            var key = entry.Metadata.FindPrimaryKey()!.Properties.Single();
            await using var command = Database.GetDbConnection().CreateCommand();
            command.Transaction = Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = $"SELECT 1 FROM \"{entry.Metadata.GetTableName()}\" WHERE \"{key.GetColumnName(store)}\" = @id LIMIT 1";
            var parameter = command.CreateParameter(); parameter.ParameterName = "@id"; parameter.Value = entry.Entity.Id.ToString(); command.Parameters.Add(parameter);
            return await command.ExecuteScalarAsync(cancellationToken) is not null;
        }
    }

    private static Account Account(Guid tenant, string code, AccountType type, bool control, bool direct) => new() { Id = Guid.NewGuid(), TenantId = tenant, AccountCode = code, AccountNumber = code, AccountName = code, AccountType = type, Status = AccountStatus.Active, CurrencyCode = "GHS", IsControlAccount = control, AllowDirectPosting = direct };
    private static string Hash(char value) => new(value, 64);
    private static async Task CreateSchemaAsync(ApplicationDbContext db)
    {
        Type[] types = SnapshotTypes;
        foreach (var type in types)
        {
            var entity = db.Model.FindEntityType(type)!; var store = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
            var columns = entity.GetProperties().Select(p => new { Name = p.GetColumnName(store)!, Type = SqlType(p.ClrType) }).GroupBy(x => x.Name).Select(x => x.First()).ToList();
            var key = entity.FindPrimaryKey()!.Properties.Select(p => $"\"{p.GetColumnName(store)}\"");
            await db.Database.ExecuteSqlRawAsync($"CREATE TABLE \"{entity.GetTableName()}\" ({string.Join(", ", columns.Select(x => $"\"{x.Name}\" {x.Type} DEFAULT {SqlDefault(x.Type)}"))}, PRIMARY KEY ({string.Join(", ", key)}));");
        }
    }
    // C14: SQL Server's production database uses case-insensitive text comparison; make the
    // SQLite harness match that provider behavior for producer/replay identity predicates.
    private static string SqlType(Type type) { type = Nullable.GetUnderlyingType(type) ?? type; return type == typeof(byte[]) ? "BLOB" : type == typeof(decimal) || type == typeof(double) || type == typeof(float) ? "REAL" : type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(bool) || type.IsEnum ? "INTEGER" : "TEXT COLLATE NOCASE"; }
    private static string SqlDefault(string sqlType) => sqlType switch { "BLOB" => "X'0000000000000000'", "REAL" => "0", "INTEGER" => "0", _ => "''" };
    private static readonly Type[] SnapshotTypes = [typeof(Tenant), typeof(FiscalYear), typeof(FiscalPeriod), typeof(ModuleDefinition), typeof(AccountingBook), typeof(AccountingBookPeriod), typeof(Account), typeof(AccountAccountingBook), typeof(AccountClassification), typeof(FinanceDimensionDefinition), typeof(FinanceDimensionValue), typeof(FinanceDimensionAccountRule), typeof(BusinessPartner), typeof(FinanceSettings), typeof(Invoice), typeof(ReturnOrder), typeof(CreditNote), typeof(CreditNoteLine), typeof(FinancePostingEvent), typeof(JournalEntry), typeof(AccountTransaction), typeof(AccountBalance), typeof(AccountCurrencyExposure), typeof(AccountingEvent), typeof(AccountingEventPosting), typeof(AccountingEventAttempt), typeof(AccountingEventProducerReceipt), typeof(AuditLog), typeof(ProducerIntentGroupMember), typeof(ProducerIntentGroupReceipt), typeof(AccountingBookSelectionEvidence), typeof(AccountingBookSelectionEvidenceBook)];
    private static async Task<IReadOnlyList<string>> MappedRowsAsync(ApplicationDbContext db)
    {
        var rows = new List<string>();
        foreach (var type in SnapshotTypes)
        {
            var table = db.Model.FindEntityType(type)!.GetTableName()!;
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = $"SELECT * FROM \"{table}\"";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                rows.Add(table + ":" + string.Join("|", Enumerable.Range(0, reader.FieldCount).Select(i => reader.GetName(i) + "=" + (reader.IsDBNull(i) ? "<null>" : reader.GetValue(i) is byte[] bytes ? Convert.ToHexString(bytes) : Convert.ToString(reader.GetValue(i))))));
        }
        return rows.OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }
    private static bool IsFailureEvidenceRow(string row) => row.StartsWith("AccountingEvents:", StringComparison.Ordinal)
        || row.StartsWith("AccountingEventAttempts:", StringComparison.Ordinal)
        || row.StartsWith("AuditLogs:", StringComparison.Ordinal);
}
