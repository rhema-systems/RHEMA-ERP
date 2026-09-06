using System.Data.Common;
using System.Text.RegularExpressions;
using ErpSystem.Api.Services.Finance.Settings;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class AccountingBookApplicabilityC5ConcurrencySqlServerTests
{
    [SqlServerFact]
    public async Task AuditFailureRollsBackPolicyRuleAndBookEvidence()
    {
        await using var database = await DisposableDatabase.CreateAsync();
        var authority = await SeedAsync(database);
        await using var context = database.Context();
        var audit = new Mock<IFinanceAuditService>();
        audit.Setup(item => item.RecordAsync(It.IsAny<FinanceAuditEventDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("audit unavailable"));

        await FluentActions.Awaiting(() => Service(context, authority.TenantId, Guid.NewGuid(), audit.Object)
            .CreateDraftAsync(Draft(authority.BookId))).Should().ThrowAsync<InvalidOperationException>().WithMessage("audit unavailable");

        await using var verify = database.Context();
        (await verify.AccountingBookApplicabilityPolicies.CountAsync()).Should().Be(0);
        (await verify.AccountingBookApplicabilityRules.CountAsync()).Should().Be(0);
        (await verify.AccountingBookApplicabilityRuleBooks.CountAsync()).Should().Be(0);
    }

    [SqlServerFact]
    public async Task ConcurrentIdenticalFreezeCoordinatesBothOpenTransactions_AndPersistsOneExactEvidenceGraph()
    {
        await using var database = await DisposableDatabase.CreateAsync();
        var authority = await SeedAsync(database);
        AccountingBookSelectionDto preview;
        await using (var context = database.Context()) preview = await Service(context, authority.TenantId, Guid.NewGuid()).ResolveAsync(Input());
        var request = Freeze("race", preview, "GOODS.RECEIPT");
        var barrier = new FreezeApplicationLockBarrier(expectedParticipants: 2);

        async Task<AccountingBookSelectionDto> FreezeOnce()
        {
            await using var context = database.Context(barrier);
            return await Service(context, authority.TenantId, Guid.NewGuid()).FreezeAsync(request);
        }
        var results = await Task.WhenAll(Task.Run(FreezeOnce), Task.Run(FreezeOnce));
        barrier.Arrivals.Should().Be(2);
        results[0].SelectionFingerprint.Should().Be(results[1].SelectionFingerprint);
        await using var verify = database.Context();
        (await verify.AccountingBookSelectionEvidence.CountAsync()).Should().Be(1);
        (await verify.AccountingBookSelectionEvidenceBooks.CountAsync()).Should().Be(1);
        var evidence = await verify.AccountingBookSelectionEvidence.Include(item => item.Books).SingleAsync();
        evidence.Books.Single().AccountingBookId.Should().Be(authority.BookId);
        evidence.Books.Single().SelectionOrder.Should().Be(0);
    }

    [SqlServerFact]
    public async Task ConcurrentConflictingFreezeCoordinatesBothOpenTransactions_AndOneConflictLeavesNoPartialRows()
    {
        await using var database = await DisposableDatabase.CreateAsync();
        var authority = await SeedAsync(database);
        AccountingBookSelectionDto firstPreview; AccountingBookSelectionDto secondPreview;
        await using (var context = database.Context()) firstPreview = await Service(context, authority.TenantId, Guid.NewGuid()).ResolveAsync(Input());
        var otherInput = Input(); otherInput.SourceDocumentType = "OTHER.DOCUMENT";
        await using (var context = database.Context()) secondPreview = await Service(context, authority.TenantId, Guid.NewGuid()).ResolveAsync(otherInput);
        var barrier = new FreezeApplicationLockBarrier(expectedParticipants: 2);

        async Task<(AccountingBookSelectionDto? Result, Exception? Error)> Attempt(FreezeAccountingBookSelectionDto request)
        {
            await using var context = database.Context(barrier);
            try { return (await Service(context, authority.TenantId, Guid.NewGuid()).FreezeAsync(request), null); }
            catch (Exception error) { return (null, error); }
        }
        var outcomes = await Task.WhenAll(
            Task.Run(() => Attempt(Freeze("race", firstPreview, "GOODS.RECEIPT"))),
            Task.Run(() => Attempt(Freeze("RACE", secondPreview, "OTHER.DOCUMENT"))));
        barrier.Arrivals.Should().Be(2);
        outcomes.Should().ContainSingle(item => item.Result != null && item.Error == null);
        outcomes.Count(item => item.Error is InvalidOperationException exception
            && exception.Message.StartsWith("ACCOUNTING_BOOK_SELECTION_IDEMPOTENCY_CONFLICT:", StringComparison.Ordinal)).Should().Be(1);
        await using var final = database.Context();
        (await final.AccountingBookSelectionEvidence.CountAsync()).Should().Be(1);
        (await final.AccountingBookSelectionEvidenceBooks.CountAsync()).Should().Be(1);
    }

    private static async Task<(Guid TenantId, Guid BookId)> SeedAsync(DisposableDatabase database)
    {
        await using var db = database.Context(); await db.Database.EnsureCreatedAsync();
        var tenant = Guid.NewGuid(); var book = Guid.NewGuid(); var year = Guid.NewGuid(); var period = Guid.NewGuid();
        var account = Guid.NewGuid(); var classification = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenant, Code = $"C5{tenant:N}"[..12].ToUpperInvariant(), Name = "C5 race", Status = TenantStatus.Active, BaseCurrency = "GHS" });
        db.AccountingBooks.Add(new AccountingBook { Id = book, TenantId = tenant, Code = "IFRS", Name = "IFRS", Purpose = "Reporting",
            BookType = AccountingBookType.PrimaryFull, LifecycleStatus = AccountingBookLifecycleStatus.Active, FunctionalCurrencyCode = "GHS",
            IsDefault = true, IsActive = true, AllowsPosting = true });
        db.FiscalYears.Add(new FiscalYear { Id = year, TenantId = tenant, FiscalYearName = "FY26", FiscalYearCode = "2026", Year = 2026,
            FiscalYearType = "Calendar", StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31), TotalDays = 365,
            NumberOfPeriods = 12, Status = "Open", IsActive = true });
        db.FiscalPeriods.Add(new FiscalPeriod { Id = period, TenantId = tenant, FiscalYearId = year, PeriodName = "September", PeriodCode = "2026-09",
            PeriodNumber = 9, StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2026, 9, 30), PeriodDays = 30, PeriodStatus = "Open", IsOpen = true });
        db.ModuleDefinitions.Add(new ModuleDefinition { TenantId = tenant, ModuleCode = "INV", ModuleName = "Inventory", IsActive = true });
        db.AccountingBookPeriods.Add(new AccountingBookPeriod { TenantId = tenant, AccountingBookId = book, FiscalPeriodId = period, PeriodStatus = AccountingBookPeriodStatus.Open });
        db.Accounts.Add(new Account { Id = account, TenantId = tenant, AccountCode = "1000", AccountNumber = "1000", AccountName = "Asset",
            AccountType = AccountType.Asset, CurrencyCode = "GHS", Status = AccountStatus.Active });
        db.AccountClassifications.Add(new AccountClassification { Id = classification, TenantId = tenant, AccountingBookId = book, Code = "ASSET",
            Name = "Asset", CoreAccountType = AccountType.Asset, Status = AccountClassificationStatus.Active, IsPostingClassification = true });
        db.AccountAccountingBooks.Add(new AccountAccountingBook { TenantId = tenant, AccountingBookId = book, AccountId = account,
            AccountClassificationId = classification, IsEnabled = true });
        await db.SaveChangesAsync(); return (tenant, book);
    }

    private static AccountingBookApplicabilityService Service(ApplicationDbContext db, Guid tenant, Guid actor, IFinanceAuditService? audit = null)
    {
        var user = new Mock<ICurrentUserService>(); user.SetupGet(item => item.TenantId).Returns(tenant); user.SetupGet(item => item.UserId).Returns(actor.ToString()); user.SetupGet(item => item.UserName).Returns("c5.sql");
        var workflow = new Mock<IWorkflowService>();
        if (audit == null)
        {
            var auditMock = new Mock<IFinanceAuditService>();
            auditMock.Setup(item => item.RecordAsync(It.IsAny<FinanceAuditEventDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(new AuditLog());
            audit = auditMock.Object;
        }
        var initialization = new Mock<IAccountingBookInitializationService>(); initialization.Setup(item => item.ValidateCurrentApprovedEvidenceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountingBookInitializationEvidenceValidationDto { IsValid = true, InitializationId = Guid.NewGuid(), Version = 1,
                EvidenceFingerprint = new string('A', 64), ReconciliationFingerprint = new string('B', 64) });
        return new AccountingBookApplicabilityService(db, user.Object, workflow.Object, audit, initialization.Object);
    }

    private static SaveAccountingBookApplicabilityPolicyDto Draft(Guid bookId) => new() { PolicyCode = "SQL_POLICY", Name = "SQL policy",
        EffectiveFrom = new DateTime(2026, 1, 1), Reason = "Test atomic audit", Rules = [new() { RuleCode = "RULE", Priority = 1,
            OriginatingModuleCode = "INV", SourceDocumentType = "GOODS.RECEIPT", PostingAction = "POST", AccountingBookIds = [bookId] }] };
    private static ResolveAccountingBookApplicabilityDto Input() => new() { EffectiveDate = new DateTime(2026, 9, 6), OriginatingModuleCode = "INV", SourceDocumentType = "GOODS.RECEIPT", PostingAction = "POST" };
    private static FreezeAccountingBookSelectionDto Freeze(string key, AccountingBookSelectionDto preview, string document) => new() { IdempotencyKey = key,
        EffectiveDate = preview.EffectiveDate, OriginatingModuleCode = "INV", SourceDocumentType = document, PostingAction = "POST",
        ExpectedCalculationInputHash = preview.CalculationInputHash, ExpectedSelectionFingerprint = preview.SelectionFingerprint };

    private sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute() { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER"))) Skip = "Set RHEMA_TEST_SQLSERVER to run prefix-safe disposable C5 service gates."; }
    }

    private sealed class FreezeApplicationLockBarrier(int expectedParticipants) : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _allArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;
        public int Arrivals => Volatile.Read(ref _arrivals);

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("sp_getapplock", StringComparison.OrdinalIgnoreCase))
            {
                // Both independent Serializable transactions have begun before this command is issued. Holding
                // them at the application-lock boundary proves they genuinely compete for the same canonical key
                // before either evidence write, instead of merely running two sequential tasks.
                if (Interlocked.Increment(ref _arrivals) == expectedParticipants) _allArrived.TrySetResult();
                await _allArrived.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }

    private sealed class DisposableDatabase : IAsyncDisposable
    {
        private static readonly Regex SafeName = new("^RHEMAERP_GL_REHEARSAL_C5_[A-Z0-9_]{1,64}$", RegexOptions.CultureInvariant);
        private readonly string _name; private readonly string _master; private readonly string _connection;
        private DisposableDatabase(string name, string master, string connection) => (_name, _master, _connection) = (name, master, connection);
        public static async Task<DisposableDatabase> CreateAsync()
        {
            var configured = Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER") ?? throw new InvalidOperationException("RHEMA_TEST_SQLSERVER is required.");
            var name = $"RHEMAERP_GL_REHEARSAL_C5_{Guid.NewGuid():N}".ToUpperInvariant(); if (!SafeName.IsMatch(name)) throw new InvalidOperationException("Disposable database prefix assertion failed.");
            var master = new SqlConnectionStringBuilder(configured) { InitialCatalog = "master", TrustServerCertificate = true };
            var target = new SqlConnectionStringBuilder(configured) { InitialCatalog = name, TrustServerCertificate = true };
            var result = new DisposableDatabase(name, master.ConnectionString, target.ConnectionString);
            await result.MasterAsync($"CREATE DATABASE [{name}]"); return result;
        }
        public ApplicationDbContext Context(DbCommandInterceptor? interceptor = null)
        {
            var builder = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(_connection, options => options.EnableRetryOnFailure());
            if (interceptor != null) builder.AddInterceptors(interceptor);
            return new ApplicationDbContext(builder.Options);
        }
        private async Task MasterAsync(string sql) { await using var connection = new SqlConnection(_master); await connection.OpenAsync(); await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 }; await command.ExecuteNonQueryAsync(); }
        public async ValueTask DisposeAsync() { if (!SafeName.IsMatch(_name)) return; await MasterAsync($"IF DB_ID(N'{_name}') IS NOT NULL BEGIN ALTER DATABASE [{_name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_name}]; END"); }
    }
}
