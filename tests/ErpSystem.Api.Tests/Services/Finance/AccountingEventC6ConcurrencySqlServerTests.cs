using System.Text.RegularExpressions;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ErpSystem.Shared;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class AccountingEventC6ConcurrencySqlServerTests
{
    [SqlServerFact]
    public async Task TwoContextsSerializeIdenticalAndConflictingSubmissions()
    {
        await using var database = await DisposableDatabase.CreateAsync();
        await using (var setup = database.Context())
        {
            await setup.Database.EnsureCreatedAsync();
            setup.Tenants.Add(Tenant(database.TenantId));
            await setup.SaveChangesAsync();
        }
        var request = Request(database);
        async Task<(AccountingEventDto? Result, Exception? Error)> Prepare(CreateAccountingEventDto candidate)
        {
            await using var db = database.Context();
            try { return (await Service(db, database.TenantId, database.MakerId, Mock.Of<IAccountingBookApplicabilityService>()).CreateAsync(candidate), null); }
            catch (Exception error) { return (null, error); }
        }
        var identical = await Task.WhenAll(Task.Run(() => Prepare(request)), Task.Run(() => Prepare(Request(database))));
        identical.Should().OnlyContain(item => item.Error == null);
        identical.Select(item => item.Result!.Id).Distinct().Should().ContainSingle();
        var raceSource = Guid.NewGuid();
        var conflictA = Request(database, "EVENT-CONFLICT-RACE", raceSource); conflictA.PostingRequest.Description = "winner A";
        var conflictB = Request(database, "EVENT-CONFLICT-RACE", raceSource); conflictB.PostingRequest.Description = "winner B";
        var conflicting = await Task.WhenAll(Task.Run(() => Prepare(conflictA)), Task.Run(() => Prepare(conflictB)));
        conflicting.Count(item => item.Result is not null && item.Error is null).Should().Be(1);
        conflicting.Count(item => item.Result is null && item.Error is InvalidOperationException error
            && error.Message.StartsWith("ACCOUNTING_EVENT_IDEMPOTENCY_CONFLICT:", StringComparison.Ordinal)).Should().Be(1);
        await using var verify = database.Context();
        (await verify.AccountingEvents.CountAsync()).Should().Be(2);
        (await verify.AccountingEvents.CountAsync(x => x.IdempotencyKey == "EVENT-CONFLICT-RACE")).Should().Be(1);
    }

    [SqlServerFact]
    public async Task SecondBookFailureRollsBackEveryEconomicWrite_PersistsFailure_Retries_AndReversesExactGroup()
    {
        await using var database = await DisposableDatabase.CreateAsync();
        Seeded seeded;
        await using (var setup = database.Context()) seeded = await SeedAsync(setup, database);
        var applicability = Applicability(seeded);
        var request = Request(database);
        Guid eventId;
        await using (var prepare = database.Context())
            eventId = (await Service(prepare, database.TenantId, database.MakerId, applicability).CreateAsync(request)).Id;

        await using (var release = database.Context())
        {
            await FluentActions.Awaiting(() => Service(release, database.TenantId, database.CheckerId, applicability)
                .ReleaseAsync(eventId, new ReleaseAccountingEventDto { Reason = "independent release", Request = Request(database) }))
                .Should().ThrowAsync<InvalidOperationException>();
        }
        await using (var failed = database.Context())
        {
            var item = await failed.AccountingEvents.Include(x => x.Attempts).Include(x => x.Postings).SingleAsync(x => x.Id == eventId);
            item.Status.Should().Be(AccountingEventStatuses.Failed); item.Attempts.Should().ContainSingle(x => x.Status == AccountingEventStatuses.Failed);
            item.Postings.Should().BeEmpty();
            (await failed.JournalEntries.CountAsync()).Should().Be(0);
            (await failed.FinancePostingEvents.CountAsync()).Should().Be(0);
            (await failed.AccountBalances.CountAsync()).Should().Be(0);
            (await failed.Accounts.Where(x => x.Id == seeded.DebitId || x.Id == seeded.CreditId).SumAsync(x => x.Balance)).Should().Be(0m);
        }

        await using (var repair = database.Context())
        {
            foreach (var account in new[] { seeded.DebitId, seeded.CreditId })
                if (!await repair.AccountAccountingBooks.AnyAsync(x => x.AccountId == account && x.AccountingBookId == seeded.SecondBookId))
                    repair.AccountAccountingBooks.Add(new AccountAccountingBook { TenantId = database.TenantId, AccountId = account,
                        AccountingBookId = seeded.SecondBookId, AccountClassificationId = account == seeded.DebitId ? seeded.SecondAssetClassId : seeded.SecondLiabilityClassId,
                        IsEnabled = true });
            await repair.SaveChangesAsync();
        }
        await using (var retry = database.Context())
        {
            var posted = await Service(retry, database.TenantId, database.CheckerId, applicability)
                .ReleaseAsync(eventId, new ReleaseAccountingEventDto { Reason = "independent release", Request = Request(database) });
            posted.Status.Should().Be(AccountingEventStatuses.Posted);
            posted.Postings.Should().HaveCount(2).And.OnlyContain(x => x.Status == AccountingEventStatuses.Posted && x.EventVersion == 1);
            posted.Attempts.Should().HaveCount(2);
        }

        var reversalRequest = Request(database); reversalRequest.EventKind = AccountingEventKinds.Reversal;
        reversalRequest.PostingRequest.PostingDate = database.EventDate.AddDays(1);
        reversalRequest.ReversesAccountingEventId = eventId; reversalRequest.SupersedesAccountingEventId = eventId;
        Guid reversalId;
        await using (var reversePrepare = database.Context())
            reversalId = (await Service(reversePrepare, database.TenantId, database.MakerId, applicability).CreateAsync(reversalRequest)).Id;
        await using (var reverseRelease = database.Context())
        {
            var reversed = await Service(reverseRelease, database.TenantId, database.CheckerId, applicability)
                .ReleaseAsync(reversalId, new ReleaseAccountingEventDto { Reason = "exact group reversal", Request = reversalRequest });
            reversed.Version.Should().Be(2); reversed.RootAccountingEventId.Should().Be(eventId);
            reversed.EventDate.Should().Be(database.EventDate.AddDays(1));
            reversed.Postings.Select(x => (x.AccountingBookId, x.EventVersion)).Should().BeEquivalentTo(
                [(seeded.PrimaryBookId, 2), (seeded.SecondBookId, 2)]);
        }
        await using var final = database.Context();
        (await final.JournalEntries.CountAsync()).Should().Be(4);
        (await final.FinancePostingEvents.CountAsync()).Should().Be(4);
        (await final.AccountingEventPostings.CountAsync()).Should().Be(4);
        (await final.Accounts.Where(x => x.Id == seeded.DebitId || x.Id == seeded.CreditId)
            .Select(x => x.Balance).ToListAsync()).Should().HaveCount(2).And.OnlyContain(value => value == 0m);
        var balances = await final.AccountBalances.Where(x => x.AccountId == seeded.DebitId || x.AccountId == seeded.CreditId).ToListAsync();
        balances.Select(x => (x.AccountId, x.AccountingBookId)).Should().BeEquivalentTo(new[]
        {
            (seeded.DebitId, seeded.PrimaryBookId), (seeded.CreditId, seeded.PrimaryBookId),
            (seeded.DebitId, seeded.SecondBookId), (seeded.CreditId, seeded.SecondBookId)
        });
        balances.Should().OnlyContain(x => x.ClosingBalance == 0m && x.PeriodNetMovement == 0m && x.YearToDateNetMovement == 0m);
        (await final.AccountCurrencyExposures.CountAsync(x => x.AccountId == seeded.DebitId || x.AccountId == seeded.CreditId)).Should().Be(0);
    }

    private static AccountingEventService Service(ApplicationDbContext db, Guid tenant, Guid actor, IAccountingBookApplicabilityService applicability)
    {
        var user = User(tenant, actor); var audit = new Mock<IFinanceAuditService>();
        audit.Setup(x => x.RecordAsync(It.IsAny<FinanceAuditEventDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(new AuditLog());
        var leaf = new FinancePostingEngine(db, user.Object, NullLogger<FinancePostingEngine>.Instance, audit.Object);
        return new AccountingEventService(db, user.Object, applicability, leaf, audit.Object,
            Options.Create(new AccountingEventOptions { Enabled = true }));
    }

    private static Mock<ICurrentUserService> User(Guid tenant, Guid actor)
    {
        var user = new Mock<ICurrentUserService>(); user.SetupGet(x => x.TenantId).Returns(tenant);
        user.SetupGet(x => x.UserId).Returns(actor.ToString()); user.SetupGet(x => x.UserName).Returns("c6.sql");
        user.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>()); return user;
    }

    private static IAccountingBookApplicabilityService Applicability(Seeded seeded)
    {
        var mock = new Mock<IAccountingBookApplicabilityService>();
        mock.Setup(x => x.FreezeAsync(It.IsAny<FreezeAccountingBookSelectionDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(seeded.Selection);
        return mock.Object;
    }

    private static async Task<Seeded> SeedAsync(ApplicationDbContext db, DisposableDatabase database)
    {
        await db.Database.EnsureCreatedAsync(); var tenant = database.TenantId;
        var primary = Book(tenant, "IFRS", true, AccountingBookType.PrimaryFull); var second = Book(tenant, "LOCAL", false, AccountingBookType.ParallelFull);
        var year = new FiscalYear { TenantId = tenant, FiscalYearName = "FY26", FiscalYearCode = "2026", Year = 2026, FiscalYearType = "Calendar",
            StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31), TotalDays = 365, NumberOfPeriods = 12, Status = "Open", IsActive = true };
        var period = new FiscalPeriod { TenantId = tenant, FiscalYearId = year.Id, PeriodName = "September", PeriodCode = "2026-09", PeriodNumber = 9,
            StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2026, 9, 30), PeriodDays = 30, PeriodStatus = "Open", IsOpen = true };
        var debit = Account(tenant, "1000", AccountType.Asset); var credit = Account(tenant, "2000", AccountType.Liability);
        database.DebitId = debit.Id; database.CreditId = credit.Id;
        var pa = Classification(tenant, primary.Id, "ASSET", AccountType.Asset); var pl = Classification(tenant, primary.Id, "LIABILITY", AccountType.Liability);
        var sa = Classification(tenant, second.Id, "ASSET", AccountType.Asset); var sl = Classification(tenant, second.Id, "LIABILITY", AccountType.Liability);
        var evidence = new AccountingBookSelectionEvidence { TenantId = tenant, EffectiveDate = database.EventDate, OriginatingModuleCode = "INV",
            SourceDocumentType = "GOODS.RECEIPT", PostingAction = "POST", IdempotencyKey = "EVENT-1", CalculationInputHash = new string('A', 64),
            SelectionFingerprint = new string('B', 64), FrozenByUserId = database.MakerId, FrozenAtUtc = DateTime.UtcNow };
        evidence.Books = [EvidenceBook(tenant, evidence.Id, primary, 0, 'C'), EvidenceBook(tenant, evidence.Id, second, 1, 'D')];
        db.AddRange(Tenant(tenant), new FinanceSettings { TenantId = tenant, BaseCurrency = "GHS", CoaType = "Segmented", AccountSeparator = "-" }, primary, second,
            year, period, new ModuleDefinition { TenantId = tenant, ModuleCode = "INV", ModuleName = "Inventory", IsActive = true },
            new AccountingBookPeriod { TenantId = tenant, AccountingBookId = primary.Id, FiscalPeriodId = period.Id, PeriodStatus = AccountingBookPeriodStatus.Open },
            new AccountingBookPeriod { TenantId = tenant, AccountingBookId = second.Id, FiscalPeriodId = period.Id, PeriodStatus = AccountingBookPeriodStatus.Open },
            debit, credit, pa, pl, sa, sl,
            Mapping(tenant, primary.Id, debit.Id, pa.Id), Mapping(tenant, primary.Id, credit.Id, pl.Id), Mapping(tenant, second.Id, debit.Id, sa.Id), evidence);
        await db.SaveChangesAsync();
        return new Seeded(primary.Id, second.Id, debit.Id, credit.Id, sa.Id, sl.Id, new AccountingBookSelectionDto { SelectionEvidenceId = evidence.Id,
            EffectiveDate = database.EventDate, OriginatingModuleCode = "INV", SourceDocumentType = "GOODS.RECEIPT", PostingAction = "POST",
            CalculationInputHash = new string('A', 64), SelectionFingerprint = new string('B', 64), Books = evidence.Books.OrderBy(x => x.SelectionOrder)
                .Select(x => new AccountingBookSelectionBookDto { AccountingBookId = x.AccountingBookId, AccountingBookCode = x.AccountingBookCodeSnapshot,
                    SelectionOrder = x.SelectionOrder, AuthorityFingerprint = x.AuthorityFingerprint }).ToList() });
    }

    private static CreateAccountingEventDto Request(DisposableDatabase database, string idempotencyKey = "EVENT-1", Guid? sourceDocumentId = null) => new() { SelectionIdempotencyKey = idempotencyKey,
        ExpectedCalculationInputHash = new string('A', 64), ExpectedSelectionFingerprint = new string('B', 64), PostingRequest = new FinancePostingRequestV2Dto {
            SourceModule = "INV", OriginModuleCode = "INV", SourceDocumentType = "GOODS.RECEIPT", SourceDocumentId = sourceDocumentId ?? database.SourceId,
            SourceDocumentTenantId = database.TenantId, PostingAction = "POST", PostingDate = database.EventDate, FunctionalCurrencyCode = "GHS",
            Description = "C6 SQL event", Lines = [new FinancePostingLineDto { AccountId = database.DebitId, DebitAmount = 100m, LineNumber = 1 },
                new FinancePostingLineDto { AccountId = database.CreditId, CreditAmount = 100m, LineNumber = 2 }] } };

    private static Tenant Tenant(Guid id) => new() { Id = id, Code = $"C6{id:N}"[..12].ToUpperInvariant(), Name = "C6 SQL", Status = TenantStatus.Active, BaseCurrency = "GHS" };
    private static AccountingBook Book(Guid tenant, string code, bool primary, AccountingBookType type) => new() { TenantId = tenant, Code = code, Name = code,
        Purpose = "Reporting", BookType = type, LifecycleStatus = AccountingBookLifecycleStatus.Active, FunctionalCurrencyCode = "GHS",
        IsDefault = primary, IsActive = true, AllowsPosting = true };
    private static Account Account(Guid tenant, string code, AccountType type) => new() { TenantId = tenant, AccountCode = code, AccountNumber = code,
        AccountName = code, AccountType = type, CurrencyCode = "GHS", Status = AccountStatus.Active, AllowDirectPosting = true };
    private static AccountClassification Classification(Guid tenant, Guid book, string code, AccountType type) => new() { TenantId = tenant, AccountingBookId = book,
        Code = code, Name = code, CoreAccountType = type, Status = AccountClassificationStatus.Active, IsPostingClassification = true };
    private static AccountAccountingBook Mapping(Guid tenant, Guid book, Guid account, Guid classification) => new() { TenantId = tenant, AccountingBookId = book,
        AccountId = account, AccountClassificationId = classification, IsEnabled = true };
    private static AccountingBookSelectionEvidenceBook EvidenceBook(Guid tenant, Guid evidence, AccountingBook book, int order, char fingerprint) => new() {
        TenantId = tenant, AccountingBookSelectionEvidenceId = evidence, AccountingBookId = book.Id, SelectionOrder = order,
        AccountingBookCodeSnapshot = book.Code, AuthorityFingerprint = new string(fingerprint, 64) };

    private sealed record Seeded(Guid PrimaryBookId, Guid SecondBookId, Guid DebitId, Guid CreditId, Guid SecondAssetClassId,
        Guid SecondLiabilityClassId, AccountingBookSelectionDto Selection);

    private sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute() { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER"))) Skip = "Set RHEMA_TEST_SQLSERVER to run exact-prefix disposable C6 service gates."; }
    }

    private sealed class DisposableDatabase : IAsyncDisposable
    {
        private static readonly Regex SafeName = new("^RHEMAERP_GL_REHEARSAL_C6_[A-Z0-9_]{1,64}$", RegexOptions.CultureInvariant);
        private readonly string _name; private readonly string _master; private readonly string _connection;
        public Guid TenantId { get; } = Guid.NewGuid(); public Guid MakerId { get; } = Guid.NewGuid(); public Guid CheckerId { get; } = Guid.NewGuid();
        public Guid SourceId { get; } = Guid.NewGuid(); public Guid DebitId { get; set; } public Guid CreditId { get; set; }
        public DateTime EventDate { get; } = new(2026, 9, 7);
        private DisposableDatabase(string name, string master, string connection) => (_name, _master, _connection) = (name, master, connection);
        public static async Task<DisposableDatabase> CreateAsync() { var configured = Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")!;
            var name = $"RHEMAERP_GL_REHEARSAL_C6_{Guid.NewGuid():N}".ToUpperInvariant(); if (!SafeName.IsMatch(name)) throw new InvalidOperationException("C6 prefix refusal.");
            var master = new SqlConnectionStringBuilder(configured) { InitialCatalog = "master", TrustServerCertificate = true };
            var target = new SqlConnectionStringBuilder(configured) { InitialCatalog = name, TrustServerCertificate = true };
            var db = new DisposableDatabase(name, master.ConnectionString, target.ConnectionString); await db.MasterAsync($"CREATE DATABASE [{name}]"); return db; }
        public ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(_connection).Options);
        private async Task MasterAsync(string sql) { await using var c = new SqlConnection(_master); await c.OpenAsync(); await using var cmd = new SqlCommand(sql, c) { CommandTimeout = 120 }; await cmd.ExecuteNonQueryAsync(); }
        public async ValueTask DisposeAsync() { if (SafeName.IsMatch(_name)) await MasterAsync($"IF DB_ID(N'{_name}') IS NOT NULL BEGIN ALTER DATABASE [{_name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_name}]; END"); }
    }
}
