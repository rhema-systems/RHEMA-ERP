using System.Data;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
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
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

/// <summary>
/// Relational owner proof for the Sales C7/C11/C12 handoff.  SQLite cannot execute the
/// SQL Server C6 posting leaf, so <see cref="C6CompatibilityLeaf"/> is deliberately the
/// only representation seam; it writes the same event/posting/journal identities inside
/// the caller transaction.  Preparation, independent decision, immutable snapshots and
/// producer compatibility resolution are the real Finance services.
/// </summary>
public sealed class ArCreditNoteProducerRelationalFixtureTests
{
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
        await fixture.Producer.ApprovePreparedAsync(pending.Id,
            new DecideProducerAccountingIntentDto { Reason = "Independent Sales checker approval" });
        fixture.UseMaker();

        await using var callerTransaction = await fixture.Db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var posted = await fixture.Service.PostCreditNoteAsync(fixture.CreditNote.Id);
        fixture.Db.Database.CurrentTransaction.Should().NotBeNull("the owner joined the caller-owned transaction");
        await callerTransaction.CommitAsync();

        posted.JournalEntryId.Should().NotBeNull();
        fixture.Db.ChangeTracker.Clear();
        var durable = await fixture.Db.AccountingEvents.Include(x => x.Postings).SingleAsync();
        durable.Status.Should().Be(AccountingEventStatuses.Posted);
        durable.Postings.Should().ContainSingle(x => x.FinancePostingEventId.HasValue && x.JournalEntryId.HasValue);
        (await fixture.Db.CreditNotes.SingleAsync()).JournalEntryId.Should().Be(posted.JournalEntryId);

        var retry = await fixture.Service.PostCreditNoteAsync(fixture.CreditNote.Id);
        retry.JournalEntryId.Should().Be(posted.JournalEntryId);
        fixture.Leaf.Executions.Should().Be(1, "an exact retry is Finance and owner read-only");
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
        fixture.Leaf.FailAfterWriting = true;

        await FluentActions.Awaiting(() => fixture.Service.PostCreditNoteAsync(fixture.CreditNote.Id))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("injected C6 compatibility leaf failure");

        fixture.Db.ChangeTracker.Clear();
        (await fixture.Db.CreditNotes.SingleAsync()).JournalEntryId.Should().BeNull();
        (await fixture.Db.FinancePostingEvents.CountAsync()).Should().Be(0);
        (await fixture.Db.JournalEntries.CountAsync()).Should().Be(0);
        var failed = await fixture.Db.AccountingEvents.Include(x => x.Attempts).SingleAsync();
        failed.Status.Should().Be(AccountingEventStatuses.Failed);
        failed.Attempts.Should().ContainSingle(x => x.Status == AccountingEventStatuses.Failed);
        fixture.Db.Database.CurrentTransaction.Should().BeNull();
        fixture.Db.ChangeTracker.Entries().Should().OnlyContain(x => x.State == EntityState.Unchanged);

        fixture.Leaf.FailAfterWriting = false;
        var repaired = await fixture.Service.PostCreditNoteAsync(fixture.CreditNote.Id);
        repaired.JournalEntryId.Should().NotBeNull();
        (await fixture.Db.AccountingEvents.Include(x => x.Attempts).SingleAsync()).Attempts
            .Count(x => x.Status == AccountingEventStatuses.Failed).Should().Be(1);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly Mock<ICurrentUserService> _currentUser;
        private readonly Guid _maker;
        private readonly Guid _checker;
        public ApplicationDbContext Db { get; }
        public CreditNote CreditNote { get; }
        public ReturnOrderService Service { get; }
        public FinanceProducerIntentService Producer { get; }
        public C6CompatibilityLeaf Leaf { get; }

        private Fixture(SqliteConnection connection, ApplicationDbContext db, CreditNote creditNote,
            ReturnOrderService service, FinanceProducerIntentService producer, C6CompatibilityLeaf leaf,
            Mock<ICurrentUserService> currentUser, Guid maker, Guid checker)
        { _connection = connection; Db = db; CreditNote = creditNote; Service = service; Producer = producer; Leaf = leaf; _currentUser = currentUser; _maker = maker; _checker = checker; }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await CreateSchemaAsync(db);
            var tenant = Guid.NewGuid(); var maker = Guid.NewGuid(); var checker = Guid.NewGuid();
            var user = new Mock<ICurrentUserService>();
            user.SetupGet(x => x.TenantId).Returns(tenant); user.SetupGet(x => x.UserId).Returns(() => maker.ToString());
            user.SetupGet(x => x.UserName).Returns("sales-maker"); user.SetupGet(x => x.IsAuthenticated).Returns(true);
            var book = new AccountingBook { Id = Guid.NewGuid(), TenantId = tenant, Code = "IFRS", Name = "IFRS", IsDefault = true, IsActive = true, AllowsPosting = true, BookType = AccountingBookType.PrimaryFull, FunctionalCurrencyCode = "GHS" };
            var ar = Account(tenant, "1200", AccountType.Asset, true, false); var returns = Account(tenant, "5200", AccountType.Expense, false, true);
            var partner = new BusinessPartner { Id = Guid.NewGuid(), TenantId = tenant, PartnerCode = "C1", PartnerName = "Relational customer", PartnerType = "Customer", IsActive = true, DefaultArAccountId = ar.Id };
            var credit = new CreditNote { Id = Guid.NewGuid(), TenantId = tenant, BusinessPartnerId = partner.Id, DocumentNumber = "SCN-R1", DocumentDate = new DateTime(2026, 9, 1), Currency = "GHS", ExchangeRate = 1, CreditNoteStatus = CreditNoteStatus.Approved, TotalAmount = 100, CreatedAt = DateTime.UtcNow, CreatedBy = "seed" };
            credit.Lines.Add(new CreditNoteLine { Id = Guid.NewGuid(), TenantId = tenant, CreditNoteId = credit.Id, Description = "Return", Quantity = 1, UnitPrice = 100, CreatedAt = DateTime.UtcNow, CreatedBy = "seed" });
            db.AddRange(book, ar, returns, partner, credit, new FinanceSettings { Id = Guid.NewGuid(), TenantId = tenant, BaseCurrency = "GHS", ControlAccountArId = ar.Id, DiscountAllowedAccountId = returns.Id });
            await db.SaveChangesAsync();
            var applicability = new Mock<IAccountingBookApplicabilityService>();
            applicability.Setup(x => x.ResolveAsync(It.IsAny<ResolveAccountingBookApplicabilityDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(new AccountingBookSelectionDto { CalculationInputHash = Hash('A'), SelectionFingerprint = Hash('B'), Books = [new AccountingBookSelectionBookDto { AccountingBookId = book.Id, AccountingBookCode = book.Code, SelectionOrder = 1, AuthorityFingerprint = Hash('C') }] });
            var audit = new Mock<IFinanceAuditService>(); audit.Setup(x => x.RecordAsync(It.IsAny<FinanceAuditEventDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(new AuditLog());
            var events = new AccountingEventService(db, user.Object, applicability.Object,
                new FinancePostingEngine(db, user.Object, NullLogger<FinancePostingEngine>.Instance), audit.Object,
                Options.Create(new AccountingEventOptions { Enabled = true }));
            var leaf = new C6CompatibilityLeaf(db, tenant, book);
            var producer = new FinanceProducerIntentService(applicability.Object, events, leaf, db, user.Object,
                Options.Create(new FinanceProducerIntentOptions { Enabled = true }));
            var provider = new Mock<ICurrentUserProvider>(); provider.SetupGet(x => x.TenantId).Returns(tenant); provider.SetupGet(x => x.UserId).Returns(maker); provider.SetupGet(x => x.Username).Returns("sales-maker");
            var workflow = new Mock<IWorkflowIntegrationService>();
            var service = new ReturnOrderService(new GenericRepository<ReturnOrder>(db), new GenericRepository<ReturnOrderLine>(db), new GenericRepository<CreditNote>(db), new GenericRepository<CreditNoteLine>(db), new GenericRepository<Refund>(db), new UnitOfWork(db), provider.Object, Mock.Of<IDocumentNumberingService>(), workflow.Object,
                new WorkflowStatusAdapterRegistry([new CreditNoteWorkflowStatusAdapter(), new RefundWorkflowStatusAdapter()]), NullLogger<ReturnOrderService>.Instance,
                financeAuditService: new FinanceAuditService(db, user.Object, new HttpContextAccessor { HttpContext = new DefaultHttpContext() }), financeProducerIntents: producer, financeProducerExecution: producer, financeProducerReversals: producer);
            return new Fixture(connection, db, credit, service, producer, leaf, user, maker, checker);
        }

        public void UseChecker() => _currentUser.SetupGet(x => x.UserId).Returns(_checker.ToString());
        public void UseMaker() => _currentUser.SetupGet(x => x.UserId).Returns(_maker.ToString());
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await _connection.DisposeAsync(); }
    }

    private sealed class C6CompatibilityLeaf(ApplicationDbContext db, Guid tenant, AccountingBook book) : ITrustedAccountingEventExecutor
    {
        public bool FailAfterWriting { get; set; }
        public int Executions { get; private set; }
        public Task<AccountingEventDto> PrepareGroupMemberInAmbientTransactionAsync(CreateAccountingEventDto request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AccountingEventDto> ValidatePreparedGroupMemberAsync(Guid accountingEventId, CreateAccountingEventDto request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AccountingEventDto> ExecuteApprovedGroupMemberInAmbientTransactionAsync(Guid accountingEventId, ReleaseAccountingEventDto request, Guid producerIntentGroupId, Guid approvedCheckerId, Guid expectedAmbientTransactionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public async Task<AccountingEventDto> ExecuteApprovedInAmbientTransactionAsync(Guid id, ReleaseAccountingEventDto request, ProducerOwnerEffectReceiptDto receipt, CancellationToken cancellationToken = default)
        {
            db.Database.CurrentTransaction.Should().NotBeNull(); Executions++;
            var item = await db.AccountingEvents.Include(x => x.Postings).SingleAsync(x => x.Id == id, cancellationToken);
            var posting = new FinancePostingEvent { Id = Guid.NewGuid(), TenantId = tenant, AccountingBookId = book.Id, SourceModule = "AR", SourceDocumentType = "SalesCreditNote", SourceDocumentId = request.Request.PostingRequest.SourceDocumentId, PostingAction = "Post", PostingStatus = "Posted", IdempotencyKey = request.Request.SelectionIdempotencyKey, PostingDate = request.Request.PostingRequest.PostingDate, RequestedAt = DateTime.UtcNow, PostedAt = DateTime.UtcNow, FunctionalCurrencyCode = "GHS", TotalDebitAmount = 100, TotalCreditAmount = 100, CreatedAt = DateTime.UtcNow };
            var journal = new JournalEntry { Id = Guid.NewGuid(), TenantId = tenant, JournalEntryNumber = $"AE-{id:N}", JournalType = "AR", EntryDate = posting.PostingDate, PostingDate = posting.PostingDate, PostingStatus = "Posted", ApprovalStatus = "Approved", AccountingBookId = book.Id, TotalDebitAmount = 100, TotalCreditAmount = 100, IsBalanced = true, CreatedAt = DateTime.UtcNow };
            item.Status = AccountingEventStatuses.Posted; item.CompletedAtUtc = DateTime.UtcNow;
            item.Postings = [new AccountingEventPosting { TenantId = tenant, AccountingEventId = id, EventVersion = item.Version, AccountingBookId = book.Id, SelectionOrder = 1, AccountingBookCodeSnapshot = book.Code, AuthorityFingerprint = Hash('C'), Status = AccountingEventStatuses.Posted, FinancePostingEventId = posting.Id, JournalEntryId = journal.Id, PostedAtUtc = DateTime.UtcNow }];
            db.AddRange(posting, journal); await db.SaveChangesAsync(cancellationToken);
            if (FailAfterWriting) throw new InvalidOperationException("injected C6 compatibility leaf failure");
            return AccountingEventService.Map(item);
        }
        public async Task RecordApprovedFailureAfterRollbackAsync(Guid id, ReleaseAccountingEventDto request, ProducerOwnerEffectReceiptDto receipt, Exception failure, CancellationToken cancellationToken = default)
        {
            db.Database.CurrentTransaction.Should().BeNull(); var item = await db.AccountingEvents.Include(x => x.Attempts).SingleAsync(x => x.Id == id, cancellationToken);
            item.Status = AccountingEventStatuses.Failed; item.FailureMessage = failure.Message; item.Attempts.Add(new AccountingEventAttempt { TenantId = tenant, AccountingEventId = id, AttemptNumber = item.Attempts.Count + 1, RequestFingerprint = item.RequestFingerprint, Status = AccountingEventStatuses.Failed, StartedAtUtc = DateTime.UtcNow, CompletedAtUtc = DateTime.UtcNow, FailureMessage = failure.Message, CreatedAt = DateTime.UtcNow }); await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static Account Account(Guid tenant, string code, AccountType type, bool control, bool direct) => new() { Id = Guid.NewGuid(), TenantId = tenant, AccountCode = code, AccountNumber = code, AccountName = code, AccountType = type, Status = AccountStatus.Active, CurrencyCode = "GHS", IsControlAccount = control, AllowDirectPosting = direct };
    private static string Hash(char value) => new(value, 64);
    private static async Task CreateSchemaAsync(ApplicationDbContext db)
    {
        Type[] types = [typeof(Tenant), typeof(AccountingBook), typeof(Account), typeof(BusinessPartner), typeof(FinanceSettings), typeof(Invoice), typeof(ReturnOrder), typeof(CreditNote), typeof(CreditNoteLine), typeof(FinancePostingEvent), typeof(JournalEntry), typeof(AccountingEvent), typeof(AccountingEventPosting), typeof(AccountingEventAttempt), typeof(AccountingEventProducerReceipt), typeof(AuditLog), typeof(ProducerIntentGroupMember)];
        foreach (var type in types)
        {
            var entity = db.Model.FindEntityType(type)!; var store = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
            var columns = entity.GetProperties().Select(p => new { Name = p.GetColumnName(store)!, Type = SqlType(p.ClrType) }).GroupBy(x => x.Name).Select(x => x.First()).ToList();
            var key = entity.FindPrimaryKey()!.Properties.Select(p => $"\"{p.GetColumnName(store)}\"");
            // ApplicationDbContext maps a few SQL Server store-generated columns.  SQLite has no
            // corresponding defaults, so provide harmless provider-local defaults for this bounded
            // fixture rather than pretending EnsureCreated is a migration rehearsal.
            await db.Database.ExecuteSqlRawAsync($"CREATE TABLE \"{entity.GetTableName()}\" ({string.Join(", ", columns.Select(x => $"\"{x.Name}\" {x.Type} DEFAULT {SqlDefault(x.Type)}"))}, PRIMARY KEY ({string.Join(", ", key)}));");
        }
    }
    private static string SqlType(Type type) { type = Nullable.GetUnderlyingType(type) ?? type; return type == typeof(byte[]) ? "BLOB" : type == typeof(decimal) || type == typeof(double) || type == typeof(float) ? "REAL" : type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(bool) || type.IsEnum ? "INTEGER" : "TEXT"; }
    private static string SqlDefault(string sqlType) => sqlType switch { "BLOB" => "X''", "REAL" => "0", "INTEGER" => "0", _ => "''" };
}
