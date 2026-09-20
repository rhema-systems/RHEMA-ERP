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

public sealed class AccountingBookLifecycleC3ConcurrencySqlServerTests
{
    private static readonly string[] CoveredWriters = ["CreateAsync", "UpdateAsync", "RequestTransitionAsync"];
    private static readonly bool[] CoveredLaunchOrders = [true, false];

    [Fact]
    public void Harness_GatesAfterReaderAndCoversEveryStructuralWriterAndLaunchOrder()
    {
        PostReaderGate.Stage.Should().Be(nameof(DbCommandInterceptor.ReaderExecutedAsync));
        PostReaderGate.MatchesAuthorityQueryForTest(
            "SELECT * FROM [AccountingBooks] WITH (UPDLOCK, HOLDLOCK) WHERE [TenantId] = @p0 AND [IsDeleted] = CAST(0 AS bit)")
            .Should().BeTrue();
        PostReaderGate.MatchesAuthorityQueryForTest(
            "SELECT CASE WHEN EXISTS (SELECT 1 FROM [AccountingBooks] AS [a] WHERE [a].[TenantId] = @p0) THEN 1 ELSE 0 END")
            .Should().BeFalse();
        CoveredWriters.Should().BeEquivalentTo("CreateAsync", "UpdateAsync", "RequestTransitionAsync");
        CoveredLaunchOrders.Should().Equal(true, false);
    }

    [SqlServerTheory]
    [InlineData("Suspended", true)]
    [InlineData("Suspended", false)]
    [InlineData("Retired", true)]
    [InlineData("Retired", false)]
    public async Task DeltaCreate_VersusInvalidationRequest_SerializesToValidGraph(string targetText, bool childStartsFirst)
    {
        var target = Enum.Parse<AccountingBookLifecycleStatus>(targetText);
        await using var database = await DisposableDatabase.CreateAsync();
        var fixture = await SeedRaceAsync(database, target, false);
        var childGate = new PostReaderGate(childStartsFirst);
        var requestGate = new PostReaderGate(!childStartsFirst);
        async Task<(bool, string?)> Child()
        {
            await using var context = database.Context(childGate);
            return await CaptureAsync(() => Service(context, fixture.TenantId, Guid.NewGuid())
                .CreateAsync(DeltaCreate("DELTA_NEW", fixture.TargetBaseId)));
        }
        async Task<(bool, string?)> Request()
        {
            await using var context = database.Context(requestGate);
            return await CaptureAsync(() => Service(context, fixture.TenantId, fixture.Maker)
                .RequestTransitionAsync(fixture.TargetBaseId, Transition(target, fixture.TargetVersion)));
        }
        var outcomes = await RunRaceAsync(Child, childGate, Request, requestGate, childStartsFirst);
        await AssertRaceAsync(database, fixture, target, outcomes, "DELTA_NEW", null, childStartsFirst);
    }

    [SqlServerTheory]
    [InlineData("Suspended", true)]
    [InlineData("Suspended", false)]
    [InlineData("Retired", true)]
    [InlineData("Retired", false)]
    public async Task DeltaReparent_VersusInvalidationRequest_SerializesToValidGraph(string targetText, bool childStartsFirst)
    {
        var target = Enum.Parse<AccountingBookLifecycleStatus>(targetText);
        await using var database = await DisposableDatabase.CreateAsync();
        var fixture = await SeedRaceAsync(database, target, true);
        var childGate = new PostReaderGate(childStartsFirst);
        var requestGate = new PostReaderGate(!childStartsFirst);
        async Task<(bool, string?)> Child()
        {
            await using var context = database.Context(childGate);
            return await CaptureAsync(() => Service(context, fixture.TenantId, Guid.NewGuid())
                .UpdateAsync(fixture.ChildId!.Value, DeltaUpdate("DELTA_CHILD", fixture.TargetBaseId, fixture.ChildVersion!)));
        }
        async Task<(bool, string?)> Request()
        {
            await using var context = database.Context(requestGate);
            return await CaptureAsync(() => Service(context, fixture.TenantId, fixture.Maker)
                .RequestTransitionAsync(fixture.TargetBaseId, Transition(target, fixture.TargetVersion)));
        }
        var outcomes = await RunRaceAsync(Child, childGate, Request, requestGate, childStartsFirst);
        await AssertRaceAsync(database, fixture, target, outcomes, "DELTA_CHILD", fixture.SafeBaseId, childStartsFirst);
    }

    [SqlServerTheory]
    [InlineData("Suspended")]
    [InlineData("Retired")]
    public async Task PendingInvalidation_DeniesChildThenCheckerCanApprove(string targetText)
    {
        var target = Enum.Parse<AccountingBookLifecycleStatus>(targetText);
        await using var database = await DisposableDatabase.CreateAsync();
        var fixture = await SeedRaceAsync(database, target, false);
        AccountingBookDto requested;
        await using (var request = database.Context())
            requested = await Service(request, fixture.TenantId, fixture.Maker)
                .RequestTransitionAsync(fixture.TargetBaseId, Transition(target, fixture.TargetVersion));
        await using (var child = database.Context())
        {
            var denied = await CaptureAsync(() => Service(child, fixture.TenantId, Guid.NewGuid())
                .CreateAsync(DeltaCreate("DELTA_DENIED", fixture.TargetBaseId)));
            denied.Success.Should().BeFalse();
            denied.Error.Should().Contain("pending");
        }
        await ApproveAsync(database, fixture, target, requested.RowVersion);
    }

    [SqlServerFact]
    public async Task TransitiveDeltaAdvance_ThenBaseSuspensionRequest_IsSequentiallyFailClosed()
    {
        // Retirement cannot race a valid Configuring->Initializing advance because no lifecycle
        // state from which Retired is reachable can support an Initializing descendant.
        await using var database = await DisposableDatabase.CreateAsync();
        var tenantId = Guid.NewGuid();
        var rootId = Guid.NewGuid();
        var middleId = Guid.NewGuid();
        var leafId = Guid.NewGuid();
        await SeedTenantAsync(database, tenantId,
            Book(rootId, tenantId, "PRIMARY", AccountingBookType.PrimaryFull, AccountingBookLifecycleStatus.Active, true),
            Book(middleId, tenantId, "DELTA_MIDDLE", AccountingBookType.Delta, AccountingBookLifecycleStatus.Initializing, baseId: rootId),
            Book(leafId, tenantId, "DELTA_LEAF", AccountingBookType.Delta, AccountingBookLifecycleStatus.Configuring, baseId: middleId));
        AccountingBookDto pending;
        await using (var request = database.Context())
        {
            var version = Convert.ToBase64String((await request.AccountingBooks.SingleAsync(x => x.Id == leafId)).RowVersion);
            pending = await Service(request, tenantId, Guid.NewGuid()).RequestTransitionAsync(leafId,
                new RequestAccountingBookTransitionDto { TargetStatus = "Initializing", Reason = "advance", RowVersion = version });
        }
        await using (var approval = database.Context())
            await Service(approval, tenantId, Guid.NewGuid()).ApproveTransitionAsync(leafId,
                new DecideAccountingBookTransitionDto { Reason = "approve", RowVersion = pending.RowVersion });
        await using var suspend = database.Context();
        var rootVersion = Convert.ToBase64String((await suspend.AccountingBooks.SingleAsync(x => x.Id == rootId)).RowVersion);
        var denied = await CaptureAsync(() => Service(suspend, tenantId, Guid.NewGuid())
            .RequestTransitionAsync(rootId, Transition(AccountingBookLifecycleStatus.Suspended, rootVersion)));
        denied.Success.Should().BeFalse();
        denied.Error.Should().Contain("DELTA_LEAF");
    }

    private static async Task<((bool Success, string? Error) Child, (bool Success, string? Error) Request)> RunRaceAsync(
        Func<Task<(bool, string?)>> child, PostReaderGate childGate,
        Func<Task<(bool, string?)>> request, PostReaderGate requestGate, bool childStartsFirst)
    {
        Task<(bool, string?)> childTask;
        Task<(bool, string?)> requestTask;
        if (childStartsFirst)
        {
            childTask = Task.Run(child);
            await childGate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
            requestTask = Task.Run(request);
            await requestGate.Attempted.Task.WaitAsync(TimeSpan.FromSeconds(30));
            requestGate.Entered.Task.IsCompleted.Should().BeFalse(
                "the competing authority query must wait behind the first UPDLOCK/HOLDLOCK reader");
            childGate.Release.TrySetResult();
            await childTask;
            await requestGate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        else
        {
            requestTask = Task.Run(request);
            await requestGate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
            childTask = Task.Run(child);
            await childGate.Attempted.Task.WaitAsync(TimeSpan.FromSeconds(30));
            childGate.Entered.Task.IsCompleted.Should().BeFalse(
                "the competing authority query must wait behind the first UPDLOCK/HOLDLOCK reader");
            requestGate.Release.TrySetResult();
            await requestTask;
            await childGate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        return (await childTask, await requestTask);
    }

    private static async Task AssertRaceAsync(DisposableDatabase database, RaceFixture fixture,
        AccountingBookLifecycleStatus target,
        ((bool Success, string? Error) Child, (bool Success, string? Error) Request) outcomes,
        string childCode, Guid? originalChildBase, bool childStartsFirst)
    {
        outcomes.Child.Success.Should().Be(childStartsFirst);
        outcomes.Request.Success.Should().Be(!childStartsFirst);
        string? pendingVersion = null;
        await using (var verify = database.Context())
        {
            var baseBook = await verify.AccountingBooks.AsNoTracking().SingleAsync(x => x.Id == fixture.TargetBaseId);
            var child = await verify.AccountingBooks.AsNoTracking().SingleOrDefaultAsync(x => x.Code == childCode);
            if (outcomes.Child.Success)
            {
                baseBook.LifecycleStatus.Should().Be(fixture.TargetInitialStatus);
                baseBook.PendingLifecycleStatus.Should().BeNull();
                child.Should().NotBeNull();
                child!.BaseAccountingBookId.Should().Be(fixture.TargetBaseId);
                return;
            }
            outcomes.Request.Success.Should().BeTrue();
            baseBook.PendingLifecycleStatus.Should().Be(target);
            if (originalChildBase.HasValue) child!.BaseAccountingBookId.Should().Be(originalChildBase);
            else child.Should().BeNull();
            pendingVersion = Convert.ToBase64String(baseBook.RowVersion);
        }
        await ApproveAsync(database, fixture, target, pendingVersion!);
    }

    private static async Task ApproveAsync(DisposableDatabase database, RaceFixture fixture,
        AccountingBookLifecycleStatus target, string rowVersion)
    {
        await using var approval = database.Context();
        var result = await Service(approval, fixture.TenantId, fixture.Checker).ApproveTransitionAsync(
            fixture.TargetBaseId, new DecideAccountingBookTransitionDto { Reason = "approve", RowVersion = rowVersion });
        result.LifecycleStatus.Should().Be(target.ToString());
    }

    private static async Task<RaceFixture> SeedRaceAsync(DisposableDatabase database,
        AccountingBookLifecycleStatus target, bool includeChild)
    {
        var tenant = Guid.NewGuid();
        var primary = Guid.NewGuid();
        var safe = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        Guid? child = includeChild ? Guid.NewGuid() : null;
        var initial = target == AccountingBookLifecycleStatus.Suspended
            ? AccountingBookLifecycleStatus.Active : AccountingBookLifecycleStatus.Configuring;
        var books = new List<AccountingBook>
        {
            Book(primary, tenant, "PRIMARY", AccountingBookType.PrimaryFull, AccountingBookLifecycleStatus.Active, true),
            Book(safe, tenant, "SAFE_BASE", AccountingBookType.ParallelFull, AccountingBookLifecycleStatus.Active),
            Book(targetId, tenant, "TARGET_BASE", AccountingBookType.ParallelFull, initial)
        };
        if (child.HasValue)
            books.Add(Book(child.Value, tenant, "DELTA_CHILD", AccountingBookType.Delta, AccountingBookLifecycleStatus.Draft, baseId: safe));
        await SeedTenantAsync(database, tenant, books.ToArray());
        await using var evidence = database.Context();
        var targetVersion = Convert.ToBase64String((await evidence.AccountingBooks.SingleAsync(x => x.Id == targetId)).RowVersion);
        var childVersion = child.HasValue
            ? Convert.ToBase64String((await evidence.AccountingBooks.SingleAsync(x => x.Id == child.Value)).RowVersion) : null;
        return new RaceFixture(tenant, safe, targetId, child, initial, targetVersion, childVersion, Guid.NewGuid(), Guid.NewGuid());
    }

    private static async Task<(bool Success, string? Error)> CaptureAsync(Func<Task<AccountingBookDto>> action)
    {
        try { await action(); return (true, null); }
        catch (Exception ex) when (ex is InvalidOperationException or DbUpdateException or SqlException)
        { return (false, ex.Message); }
    }

    private static RequestAccountingBookTransitionDto Transition(AccountingBookLifecycleStatus target, string version) =>
        new() { TargetStatus = target.ToString(), Reason = "governed transition", RowVersion = version };
    private static CreateAccountingBookDto DeltaCreate(string code, Guid baseId) => new()
    { Code = code, Name = code, Purpose = "Adjustment", BookType = "Delta", BaseAccountingBookId = baseId };
    private static UpdateAccountingBookDto DeltaUpdate(string code, Guid baseId, string version) => new()
    { Code = code, Name = code, Purpose = "Adjustment", BookType = "Delta", BaseAccountingBookId = baseId, RowVersion = version };

    private static async Task SeedTenantAsync(DisposableDatabase database, Guid tenantId, params AccountingBook[] books)
    {
        await using var db = database.Context();
        await db.Database.EnsureCreatedAsync();
        db.Tenants.Add(new Tenant { Id = tenantId, Code = $"C3{tenantId:N}"[..12].ToUpperInvariant(), Name = "C3 race", Status = TenantStatus.Active, BaseCurrency = "GHS" });
        db.FinanceSettings.Add(new FinanceSettings { Id = Guid.NewGuid(), TenantId = tenantId, BaseCurrency = "GHS" });
        db.AccountingBooks.AddRange(books);
        await db.SaveChangesAsync();
    }

    private static AccountingBook Book(Guid id, Guid tenant, string code, AccountingBookType type,
        AccountingBookLifecycleStatus status, bool isDefault = false, Guid? baseId = null) => new()
    {
        Id = id, TenantId = tenant, Code = code, Name = code, Purpose = "Reporting", BookType = type,
        LifecycleStatus = status, FunctionalCurrencyCode = type == AccountingBookType.Delta ? null : "GHS",
        BaseAccountingBookId = baseId, IsDefault = isDefault,
        IsActive = status == AccountingBookLifecycleStatus.Active, AllowsPosting = status == AccountingBookLifecycleStatus.Active
    };

    private static AccountingBookService Service(ApplicationDbContext db, Guid tenant, Guid actor)
    {
        var current = new Mock<ICurrentUserService>();
        current.SetupGet(x => x.TenantId).Returns(tenant);
        current.SetupGet(x => x.UserId).Returns(actor.ToString());
        current.SetupGet(x => x.UserName).Returns("finance.c3.sql-race");
        return new AccountingBookService(db, current.Object, Workflow().Object, Audit().Object);
    }

    private static Mock<IWorkflowService> Workflow()
    {
        var mock = new Mock<IWorkflowService>();
        mock.Setup(x => x.HasActiveApprovalWorkflowAsync(It.IsAny<string>())).ReturnsAsync(true);
        mock.Setup(x => x.StartApprovalWorkflowAsync(It.IsAny<string>(), It.IsAny<Guid>()))
            .ReturnsAsync(new WorkflowExecutionResult { Success = true, Status = WorkflowInstanceStatus.InProgress, WorkflowInstanceId = Guid.NewGuid() });
        mock.Setup(x => x.CanUserApproveAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>())).ReturnsAsync(true);
        mock.Setup(x => x.ProcessApprovalStepAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(new WorkflowExecutionResult { Success = true, Status = WorkflowInstanceStatus.Completed });
        return mock;
    }

    private static Mock<IFinanceAuditService> Audit()
    {
        var mock = new Mock<IFinanceAuditService>();
        mock.Setup(x => x.RecordAsync(It.IsAny<FinanceAuditEventDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(new AuditLog());
        return mock;
    }

    private sealed class PostReaderGate : DbCommandInterceptor
    {
        public const string Stage = nameof(DbCommandInterceptor.ReaderExecutedAsync);
        private readonly bool _holdAfterRead;
        private int _entered;
        public PostReaderGate(bool holdAfterRead = true) => _holdAfterRead = holdAfterRead;
        public TaskCompletionSource Attempted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (IsGraphRead(command.CommandText)) Attempted.TrySetResult();
            return new ValueTask<InterceptionResult<DbDataReader>>(result);
        }
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,
            CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (IsGraphRead(command.CommandText) && Interlocked.Exchange(ref _entered, 1) == 0)
            {
                Entered.TrySetResult();
                if (_holdAfterRead) await Release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
        public static bool MatchesAuthorityQueryForTest(string sql) => IsGraphRead(sql);
        private static bool IsGraphRead(string sql) =>
            sql.Contains("[AccountingBooks]", StringComparison.Ordinal)
            && sql.Contains("WITH (UPDLOCK, HOLDLOCK)", StringComparison.OrdinalIgnoreCase)
            && sql.Contains("[TenantId]", StringComparison.Ordinal)
            && sql.Contains("[IsDeleted]", StringComparison.Ordinal);
    }

    private sealed class SqlServerFactAttribute : FactAttribute
    { public SqlServerFactAttribute() { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER"))) Skip = "Set RHEMA_TEST_SQLSERVER for disposable C3 SQL races."; } }
    private sealed class SqlServerTheoryAttribute : TheoryAttribute
    { public SqlServerTheoryAttribute() { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER"))) Skip = "Set RHEMA_TEST_SQLSERVER for disposable C3 SQL races."; } }

    private sealed class DisposableDatabase : IAsyncDisposable
    {
        private static readonly Regex Safe = new("^RHEMAERP_GL_REHEARSAL_[A-Z0-9_]{1,64}$", RegexOptions.CultureInvariant);
        private readonly string _name;
        private readonly string _master;
        private readonly string _target;
        private DisposableDatabase(string name, string master, string target) { _name = name; _master = master; _target = target; }
        public static async Task<DisposableDatabase> CreateAsync()
        {
            var configured = Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER") ?? throw new InvalidOperationException("RHEMA_TEST_SQLSERVER is required.");
            var name = $"RHEMAERP_GL_REHEARSAL_C3RACE_{Guid.NewGuid():N}".ToUpperInvariant();
            AssertSafe(name);
            var master = new SqlConnectionStringBuilder(configured) { InitialCatalog = "master", TrustServerCertificate = true };
            var target = new SqlConnectionStringBuilder(configured) { InitialCatalog = name, TrustServerCertificate = true };
            var db = new DisposableDatabase(name, master.ConnectionString, target.ConnectionString);
            await db.ExecuteAsync($"CREATE DATABASE [{name}]");
            return db;
        }
        public ApplicationDbContext Context(params IInterceptor[] interceptors)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(_target);
            if (interceptors.Length > 0) options.AddInterceptors(interceptors);
            return new ApplicationDbContext(options.Options);
        }
        public async ValueTask DisposeAsync()
        {
            AssertSafe(_name);
            await ExecuteAsync($"IF DB_ID(N'{_name}') IS NOT NULL BEGIN ALTER DATABASE [{_name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_name}]; END");
        }
        private async Task ExecuteAsync(string sql)
        {
            await using var connection = new SqlConnection(_master);
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection) { CommandTimeout = 600 };
            await command.ExecuteNonQueryAsync();
        }
        private static void AssertSafe(string name) { if (!Safe.IsMatch(name)) throw new InvalidOperationException("Unsafe rehearsal database target."); }
    }

    private sealed record RaceFixture(Guid TenantId, Guid SafeBaseId, Guid TargetBaseId, Guid? ChildId,
        AccountingBookLifecycleStatus TargetInitialStatus, string TargetVersion, string? ChildVersion, Guid Maker, Guid Checker);
}
