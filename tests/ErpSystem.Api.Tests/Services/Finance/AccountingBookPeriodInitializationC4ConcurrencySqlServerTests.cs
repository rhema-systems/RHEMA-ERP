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
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class AccountingBookPeriodInitializationC4ConcurrencySqlServerTests
{
    [SqlServerFact]
    public async Task ConcurrentDuplicateBookPeriodCreate_PersistsExactlyOneAuthority()
    {
        await using var database = await DisposableDatabase.CreateAsync();
        var tenantId = Guid.NewGuid();
        var bookId = Guid.NewGuid();
        var periodId = Guid.NewGuid();
        await using (var seed = database.Context())
        {
            await seed.Database.EnsureCreatedAsync();
            seed.Tenants.Add(new Tenant { Id = tenantId, Code = "C4RACE", Name = "C4 race", Status = TenantStatus.Active, BaseCurrency = "GHS" });
            seed.FinanceSettings.Add(new FinanceSettings { Id = Guid.NewGuid(), TenantId = tenantId, BaseCurrency = "GHS" });
            seed.AccountingBooks.Add(new AccountingBook
            {
                Id = bookId, TenantId = tenantId, Code = "IFRS", Name = "IFRS", Purpose = "Reporting",
                BookType = AccountingBookType.PrimaryFull, LifecycleStatus = AccountingBookLifecycleStatus.Initializing,
                FunctionalCurrencyCode = "GHS", IsDefault = true
            });
            seed.FiscalPeriods.Add(new FiscalPeriod
            {
                Id = periodId, TenantId = tenantId, FiscalYearId = Guid.NewGuid(), PeriodName = "January 2026",
                PeriodCode = "2026-01", PeriodNumber = 1, PeriodType = PeriodType.Monthly,
                StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 1, 31), PeriodDays = 31,
                PeriodStatus = "Open", IsOpen = true
            });
            await seed.SaveChangesAsync();
        }

        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> Writer()
        {
            ready.TrySetResult();
            await start.Task;
            await using var context = database.Context();
            try
            {
                await Service(context, tenantId, Guid.NewGuid()).CreateAsync(bookId,
                    new CreateAccountingBookPeriodDto { FiscalPeriodId = periodId });
                return true;
            }
            catch (Exception ex) when (ex is InvalidOperationException or DbUpdateException or SqlException)
            {
                return false;
            }
        }

        var first = Task.Run(Writer);
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(30));
        var second = Task.Run(Writer);
        start.TrySetResult();
        var outcomes = await Task.WhenAll(first, second);

        outcomes.Count(success => success).Should().Be(1,
            "the serializable service check and unfiltered unique grain must admit one period authority only");
        await using var verify = database.Context();
        (await verify.AccountingBookPeriods.CountAsync(item => item.TenantId == tenantId
            && item.AccountingBookId == bookId && item.FiscalPeriodId == periodId)).Should().Be(1);
    }

    [SqlServerFact]
    public async Task AuditFailure_RollsBackPeriodAndInitializationMutationBoundaries()
    {
        await using var database = await DisposableDatabase.CreateAsync();
        var fixture = await SeedInitializationAuthorityAsync(database);

        await using (var periodCreate = database.Context())
        {
            await FluentActions.Awaiting(() => PeriodService(periodCreate, fixture.TenantId, Guid.NewGuid(), FailingAudit())
                .CreateAsync(fixture.BookId, new CreateAccountingBookPeriodDto { FiscalPeriodId = fixture.OpenPeriodId }))
                .Should().ThrowAsync<InvalidOperationException>().WithMessage("audit unavailable");
        }
        await using (var verify = database.Context())
            (await verify.AccountingBookPeriods.CountAsync()).Should().Be(0);

        AccountingBookPeriodDto created;
        var maker = Guid.NewGuid();
        await using (var periodSetup = database.Context())
            created = await PeriodService(periodSetup, fixture.TenantId, maker)
                .CreateAsync(fixture.BookId, new CreateAccountingBookPeriodDto { FiscalPeriodId = fixture.OpenPeriodId });
        await using (var periodRequest = database.Context())
        {
            await FluentActions.Awaiting(() => PeriodService(periodRequest, fixture.TenantId, maker, FailingAudit())
                .RequestTransitionAsync(fixture.BookId, created.Id, new RequestAccountingBookPeriodTransitionDto
                { TargetStatus = "Open", Reason = "governed open", RowVersion = created.RowVersion }))
                .Should().ThrowAsync<InvalidOperationException>().WithMessage("audit unavailable");
        }
        await using (var verify = database.Context())
        {
            var persisted = await verify.AccountingBookPeriods.SingleAsync();
            persisted.PeriodStatus.Should().Be(AccountingBookPeriodStatus.Future);
            persisted.PendingStatus.Should().BeNull();
            persisted.WorkflowInstanceId.Should().BeNull();
        }

        var request = InitializationRequest(fixture, "init-audit-fail");
        await using (var configure = database.Context())
        {
            await FluentActions.Awaiting(() => InitializationService(configure, fixture.TenantId, Guid.NewGuid(), FailingAudit())
                .ConfigureAsync(fixture.BookId, request))
                .Should().ThrowAsync<InvalidOperationException>().WithMessage("audit unavailable");
        }
        await using (var verify = database.Context())
        {
            (await verify.AccountingBookInitializations.CountAsync()).Should().Be(0);
            (await verify.AccountingBookInitializationLines.CountAsync()).Should().Be(0);
            (await verify.AccountingBooks.SingleAsync(item => item.Id == fixture.BookId)).InitializationStartedAtUtc.Should().BeNull();
        }

        AccountingBookInitializationDto submitted;
        var initializationMaker = Guid.NewGuid();
        await using (var setup = database.Context())
        {
            var service = InitializationService(setup, fixture.TenantId, initializationMaker);
            await service.ConfigureAsync(fixture.BookId, InitializationRequest(fixture, "init-decision-fail"));
            submitted = await service.SubmitAsync(fixture.BookId);
        }
        await using (var decision = database.Context())
        {
            await FluentActions.Awaiting(() => InitializationService(decision, fixture.TenantId, Guid.NewGuid(), FailingAudit())
                .ApproveAsync(fixture.BookId, new DecideAccountingBookInitializationDto
                { Reason = "checker approval", RowVersion = submitted.RowVersion }))
                .Should().ThrowAsync<InvalidOperationException>().WithMessage("audit unavailable");
        }
        await using (var verify = database.Context())
        {
            var persisted = await verify.AccountingBookInitializations.SingleAsync();
            persisted.InitializationStatus.Should().Be(AccountingBookInitializationStatus.PendingApproval);
            persisted.ApprovedByUserId.Should().BeNull();
            persisted.ApprovedAtUtc.Should().BeNull();
        }
    }

    private static async Task<InitializationFixture> SeedInitializationAuthorityAsync(DisposableDatabase database)
    {
        var tenant = Guid.NewGuid(); var book = Guid.NewGuid(); var openPeriod = Guid.NewGuid(); var cutoffPeriod = Guid.NewGuid();
        var debit = Guid.NewGuid(); var credit = Guid.NewGuid();
        await using var db = database.Context();
        await db.Database.EnsureCreatedAsync();
        db.Tenants.Add(new Tenant { Id = tenant, Code = "C4AUDIT", Name = "C4 audit", Status = TenantStatus.Active, BaseCurrency = "GHS" });
        db.FinanceSettings.Add(new FinanceSettings { Id = Guid.NewGuid(), TenantId = tenant, BaseCurrency = "GHS" });
        db.AccountingBooks.Add(new AccountingBook { Id = book, TenantId = tenant, Code = "IFRS", Name = "IFRS", Purpose = "Reporting",
            BookType = AccountingBookType.PrimaryFull, LifecycleStatus = AccountingBookLifecycleStatus.Initializing,
            FunctionalCurrencyCode = "GHS", IsDefault = true });
        db.FiscalPeriods.AddRange(
            new FiscalPeriod { Id = openPeriod, TenantId = tenant, FiscalYearId = Guid.NewGuid(), PeriodName = "January 2026", PeriodCode = "2026-01",
                PeriodNumber = 1, PeriodType = PeriodType.Monthly, StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 1, 31),
                PeriodDays = 31, PeriodStatus = "Open", IsOpen = true },
            new FiscalPeriod { Id = cutoffPeriod, TenantId = tenant, FiscalYearId = Guid.NewGuid(), PeriodName = "December 2025", PeriodCode = "2025-12",
                PeriodNumber = 12, PeriodType = PeriodType.Monthly, StartDate = new DateTime(2025, 12, 1), EndDate = new DateTime(2025, 12, 31),
                PeriodDays = 31, PeriodStatus = "Closed", IsOpen = false, IsClosed = true });
        db.Accounts.AddRange(
            new Account { Id = debit, TenantId = tenant, AccountCode = "1000", AccountNumber = "1000", AccountName = "Debit", AccountType = AccountType.Asset, CurrencyCode = "GHS", Status = AccountStatus.Active },
            new Account { Id = credit, TenantId = tenant, AccountCode = "3000", AccountNumber = "3000", AccountName = "Credit", AccountType = AccountType.Equity, CurrencyCode = "GHS", Status = AccountStatus.Active });
        var assetClass = new AccountClassification { Id = Guid.NewGuid(), TenantId = tenant, AccountingBookId = book, Code = "ASSET", Name = "Asset",
            CoreAccountType = AccountType.Asset, Status = AccountClassificationStatus.Active, IsPostingClassification = true };
        var equityClass = new AccountClassification { Id = Guid.NewGuid(), TenantId = tenant, AccountingBookId = book, Code = "EQUITY", Name = "Equity",
            CoreAccountType = AccountType.Equity, Status = AccountClassificationStatus.Active, IsPostingClassification = true };
        db.AccountClassifications.AddRange(assetClass, equityClass);
        db.AccountAccountingBooks.AddRange(
            new AccountAccountingBook { TenantId = tenant, AccountingBookId = book, AccountId = debit, AccountClassificationId = assetClass.Id, IsEnabled = true },
            new AccountAccountingBook { TenantId = tenant, AccountingBookId = book, AccountId = credit, AccountClassificationId = equityClass.Id, IsEnabled = true });
        await db.SaveChangesAsync();
        return new InitializationFixture(tenant, book, openPeriod, cutoffPeriod, debit, credit);
    }

    private static ConfigureAccountingBookInitializationDto InitializationRequest(InitializationFixture fixture, string key) => new()
    {
        Mode = "IndependentOpeningBalances", CutoffDate = new DateTime(2025, 12, 31),
        CutoffFiscalPeriodId = fixture.CutoffPeriodId, CutoffFiscalPeriodCode = "2025-12", IdempotencyKey = key, Reason = "Governed opening",
        Lines = new[]
        {
            new AccountingBookInitializationLineDto { AccountId = fixture.DebitAccountId, CurrencyCode = "GHS" },
            new AccountingBookInitializationLineDto { AccountId = fixture.CreditAccountId, CurrencyCode = "GHS" }
        }
    };

    private static AccountingBookPeriodService Service(ApplicationDbContext db, Guid tenant, Guid actor) => PeriodService(db, tenant, actor);

    private static AccountingBookPeriodService PeriodService(ApplicationDbContext db, Guid tenant, Guid actor, IFinanceAuditService? auditOverride = null)
    {
        var current = new Mock<ICurrentUserService>();
        current.SetupGet(item => item.TenantId).Returns(tenant);
        current.SetupGet(item => item.UserId).Returns(actor.ToString());
        current.SetupGet(item => item.UserName).Returns("finance.c4.sql-race");
        var workflow = new Mock<IWorkflowService>();
        workflow.Setup(item => item.HasActiveApprovalWorkflowAsync(It.IsAny<string>())).ReturnsAsync(true);
        workflow.Setup(item => item.StartApprovalWorkflowAsync(It.IsAny<string>(), It.IsAny<Guid>()))
            .ReturnsAsync(new WorkflowExecutionResult { Success = true, Status = WorkflowInstanceStatus.InProgress, WorkflowInstanceId = Guid.NewGuid() });
        return new AccountingBookPeriodService(db, current.Object, workflow.Object, auditOverride ?? Audit());
    }

    private static AccountingBookInitializationService InitializationService(ApplicationDbContext db, Guid tenant, Guid actor, IFinanceAuditService? auditOverride = null)
    {
        var current = new Mock<ICurrentUserService>(); current.SetupGet(item => item.TenantId).Returns(tenant);
        current.SetupGet(item => item.UserId).Returns(actor.ToString()); current.SetupGet(item => item.UserName).Returns("finance.c4.sql-audit");
        return new AccountingBookInitializationService(db, current.Object, Workflow().Object, auditOverride ?? Audit());
    }

    private static Mock<IWorkflowService> Workflow()
    {
        var workflow = new Mock<IWorkflowService>();
        workflow.Setup(item => item.HasActiveApprovalWorkflowAsync(It.IsAny<string>())).ReturnsAsync(true);
        workflow.Setup(item => item.StartApprovalWorkflowAsync(It.IsAny<string>(), It.IsAny<Guid>()))
            .ReturnsAsync(new WorkflowExecutionResult { Success = true, Status = WorkflowInstanceStatus.InProgress, WorkflowInstanceId = Guid.NewGuid() });
        workflow.Setup(item => item.CanUserApproveAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>())).ReturnsAsync(true);
        workflow.Setup(item => item.ProcessApprovalStepAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(new WorkflowExecutionResult { Success = true, Status = WorkflowInstanceStatus.Completed });
        return workflow;
    }

    private static IFinanceAuditService Audit()
    { var audit = new Mock<IFinanceAuditService>(); audit.Setup(item => item.RecordAsync(It.IsAny<FinanceAuditEventDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(new AuditLog()); return audit.Object; }
    private static IFinanceAuditService FailingAudit()
    { var audit = new Mock<IFinanceAuditService>(); audit.Setup(item => item.RecordAsync(It.IsAny<FinanceAuditEventDto>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("audit unavailable")); return audit.Object; }

    private sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")))
                Skip = "Set RHEMA_TEST_SQLSERVER to run the prefix-safe disposable C4 service concurrency gate.";
        }
    }

    private sealed record InitializationFixture(Guid TenantId, Guid BookId, Guid OpenPeriodId, Guid CutoffPeriodId, Guid DebitAccountId, Guid CreditAccountId);

    private sealed class DisposableDatabase : IAsyncDisposable
    {
        private static readonly Regex Safe = new("^RHEMAERP_GL_REHEARSAL_[A-Z0-9_]{1,64}$", RegexOptions.CultureInvariant);
        private readonly string _name;
        private readonly string _master;
        private readonly string _target;
        private DisposableDatabase(string name, string master, string target) => (_name, _master, _target) = (name, master, target);

        public static async Task<DisposableDatabase> CreateAsync()
        {
            var configured = Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")
                ?? throw new InvalidOperationException("RHEMA_TEST_SQLSERVER is required.");
            var name = $"RHEMAERP_GL_REHEARSAL_C4RACE_{Guid.NewGuid():N}".ToUpperInvariant();
            AssertSafe(name);
            var master = new SqlConnectionStringBuilder(configured) { InitialCatalog = "master", TrustServerCertificate = true };
            var target = new SqlConnectionStringBuilder(configured) { InitialCatalog = name, TrustServerCertificate = true };
            var database = new DisposableDatabase(name, master.ConnectionString, target.ConnectionString);
            await database.ExecuteMasterAsync($"CREATE DATABASE [{name}]");
            return database;
        }

        public ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(_target).Options);

        public async ValueTask DisposeAsync()
        {
            AssertSafe(_name);
            await ExecuteMasterAsync($"IF DB_ID(N'{_name}') IS NOT NULL BEGIN ALTER DATABASE [{_name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_name}]; END");
        }

        private async Task ExecuteMasterAsync(string sql)
        {
            await using var connection = new SqlConnection(_master);
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection) { CommandTimeout = 600 };
            await command.ExecuteNonQueryAsync();
        }

        private static void AssertSafe(string name)
        {
            if (!Safe.IsMatch(name)) throw new InvalidOperationException("Unsafe C4 rehearsal database target.");
        }
    }
}
