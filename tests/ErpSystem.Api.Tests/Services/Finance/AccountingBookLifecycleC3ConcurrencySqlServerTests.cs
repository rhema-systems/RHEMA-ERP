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

/// <summary>
/// SQL Server is required here because the invariant depends on the serializable range locks used
/// by the two independent accounting-book writers, not merely on in-process validation order.
/// </summary>
public sealed class AccountingBookLifecycleC3ConcurrencySqlServerTests
{
    [SqlServerFact]
    public async Task DeltaReparent_CommittingBeforeBaseSuspensionRequest_MakesSuspensionFailClosed()
    {
        await ReparentWinsInvalidationRaceAsync(AccountingBookLifecycleStatus.Suspended);
    }

    [SqlServerFact]
    public async Task DeltaReparent_CommittingBeforeBaseRetirementRequest_MakesRetirementFailClosed()
    {
        await ReparentWinsInvalidationRaceAsync(AccountingBookLifecycleStatus.Retired);
    }

    [SqlServerFact]
    public async Task BaseSuspensionRequestAndApproval_WinningOpenTransactionRace_MakesDeltaReparentFailClosed()
    {
        await InvalidationWinsReparentRaceAsync(AccountingBookLifecycleStatus.Suspended);
    }

    [SqlServerFact]
    public async Task BaseRetirementRequestAndApproval_WinningOpenTransactionRace_MakesDeltaReparentFailClosed()
    {
        await InvalidationWinsReparentRaceAsync(AccountingBookLifecycleStatus.Retired);
    }

    [SqlServerFact]
    public async Task TransitiveDeltaApproval_RacingBaseSuspension_PreservesValidDurableLineage()
    {
        await using var database = await DisposableDatabase.CreateAsync();
        var tenantId = Guid.NewGuid();
        var rootId = Guid.NewGuid();
        var middleId = Guid.NewGuid();
        var leafId = Guid.NewGuid();
        var maker = Guid.NewGuid();
        var checker = Guid.NewGuid();

        await using (var seed = database.Context())
        {
            await seed.Database.EnsureCreatedAsync();
            seed.Tenants.Add(new Tenant
            {
                Id = tenantId, Code = "C3RACE", Name = "C3 race", Status = TenantStatus.Active,
                BaseCurrency = "GHS"
            });
            seed.FinanceSettings.Add(new FinanceSettings
            {
                Id = Guid.NewGuid(), TenantId = tenantId, BaseCurrency = "GHS"
            });
            seed.AccountingBooks.AddRange(
                Book(rootId, tenantId, "PRIMARY", AccountingBookType.PrimaryFull,
                    AccountingBookLifecycleStatus.Active, isDefault: true),
                Book(middleId, tenantId, "DELTA_MIDDLE", AccountingBookType.Delta,
                    AccountingBookLifecycleStatus.Initializing, baseId: rootId),
                Book(leafId, tenantId, "DELTA_LEAF", AccountingBookType.Delta,
                    AccountingBookLifecycleStatus.Configuring, baseId: middleId));
            await seed.SaveChangesAsync();
        }

        var workflow = Workflow();
        var audit = Audit();
        string requestRowVersion;
        await using (var requestContext = database.Context())
        {
            var leaf = await requestContext.AccountingBooks.SingleAsync(item => item.Id == leafId);
            requestRowVersion = Convert.ToBase64String(leaf.RowVersion);
            await Service(requestContext, tenantId, maker, workflow.Object, audit.Object).RequestTransitionAsync(
                leafId,
                new RequestAccountingBookTransitionDto
                {
                    TargetStatus = "Initializing", Reason = "prepare transitive delta", RowVersion = requestRowVersion
                });
        }

        string leafRowVersion;
        string rootRowVersion;
        await using (var evidence = database.Context())
        {
            leafRowVersion = Convert.ToBase64String((await evidence.AccountingBooks.SingleAsync(item => item.Id == leafId)).RowVersion);
            rootRowVersion = Convert.ToBase64String((await evidence.AccountingBooks.SingleAsync(item => item.Id == rootId)).RowVersion);
        }

        var approvalEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseApproval = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var approvalAudit = Audit(async eventType =>
        {
            if (eventType != FinanceAuditEvents.AccountingBookTransitionApproved) return;
            approvalEntered.TrySetResult();
            await releaseApproval.Task;
        });
        var approve = Task.Run(async () =>
        {
            await using var context = database.Context();
            return await CaptureAsync(() => Service(context, tenantId, checker, workflow.Object, approvalAudit.Object)
                .ApproveTransitionAsync(leafId, new DecideAccountingBookTransitionDto
                {
                    Reason = "approve initialization", RowVersion = leafRowVersion
                }));
        });
        await approvalEntered.Task.WaitAsync(TimeSpan.FromSeconds(30));
        var suspensionTransaction = new TransactionGate();
        var suspend = Task.Run(async () =>
        {
            await using var context = database.Context();
            return await CaptureAsync(() => Service(context, tenantId, Guid.NewGuid(), workflow.Object, audit.Object,
                    suspensionTransaction.BlockOnce)
                .RequestTransitionAsync(rootId, new RequestAccountingBookTransitionDto
                {
                    TargetStatus = "Suspended", Reason = "suspend root", RowVersion = rootRowVersion
                }));
        });
        await suspensionTransaction.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
        // Advancing to Initializing can have Active/Initializing ancestors, whereas a legitimate
        // retirement-approval race is unreachable because Retired has no such source state.
        // Suspension is therefore the meaningful invalidation race for this advancement path.
        releaseApproval.TrySetResult();
        var approvalOutcome = await approve;
        suspensionTransaction.Release.TrySetResult();
        var suspensionOutcome = await suspend;

        approvalOutcome.Success.Should().BeTrue();
        suspensionOutcome.Success.Should().BeFalse();
        suspensionOutcome.Error.Should().Contain("DELTA_LEAF");
        await using var verify = database.Context();
        var durableRoot = await verify.AccountingBooks.AsNoTracking().SingleAsync(item => item.Id == rootId);
        var durableLeaf = await verify.AccountingBooks.AsNoTracking().SingleAsync(item => item.Id == leafId);
        durableRoot.LifecycleStatus.Should().Be(AccountingBookLifecycleStatus.Active);
        durableRoot.PendingLifecycleStatus.Should().BeNull();
        durableLeaf.LifecycleStatus.Should().Be(AccountingBookLifecycleStatus.Initializing);
        durableLeaf.PendingLifecycleStatus.Should().BeNull();
    }

    private static async Task<(bool Success, string? Error)> CaptureAsync(Func<Task<AccountingBookDto>> action)
    {
        try { await action(); return (true, null); }
        catch (InvalidOperationException exception) { return (false, exception.Message); }
    }

    private static async Task ReparentWinsInvalidationRaceAsync(AccountingBookLifecycleStatus target)
    {
        await using var database = await DisposableDatabase.CreateAsync();
        var tenantId = Guid.NewGuid();
        var primaryId = Guid.NewGuid();
        var safeBaseId = Guid.NewGuid();
        var targetBaseId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var targetInitial = target == AccountingBookLifecycleStatus.Suspended
            ? AccountingBookLifecycleStatus.Active
            : AccountingBookLifecycleStatus.Configuring;
        await SeedTenantAsync(database, tenantId,
            Book(primaryId, tenantId, "PRIMARY", AccountingBookType.PrimaryFull,
                AccountingBookLifecycleStatus.Active, isDefault: true),
            Book(safeBaseId, tenantId, "SAFE_BASE", AccountingBookType.ParallelFull,
                AccountingBookLifecycleStatus.Active),
            Book(targetBaseId, tenantId, "TARGET_BASE", AccountingBookType.ParallelFull, targetInitial),
            Book(childId, tenantId, "DELTA_CHILD", AccountingBookType.Delta,
                AccountingBookLifecycleStatus.Draft, baseId: safeBaseId));

        string childVersion;
        string targetVersion;
        await using (var evidence = database.Context())
        {
            childVersion = Convert.ToBase64String((await evidence.AccountingBooks.SingleAsync(item => item.Id == childId)).RowVersion);
            targetVersion = Convert.ToBase64String((await evidence.AccountingBooks.SingleAsync(item => item.Id == targetBaseId)).RowVersion);
        }

        var updateEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseUpdate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var updateAudit = Audit(async eventType =>
        {
            if (eventType != FinanceAuditEvents.AccountingBookUpdated) return;
            updateEntered.TrySetResult();
            await releaseUpdate.Task;
        });
        var update = Task.Run(async () =>
        {
            await using var context = database.Context();
            return await CaptureAsync(() => Service(context, tenantId, Guid.NewGuid(), Workflow().Object, updateAudit.Object)
                .UpdateAsync(childId, DeltaUpdate("DELTA_CHILD", targetBaseId, childVersion)));
        });
        await updateEntered.Task.WaitAsync(TimeSpan.FromSeconds(30));

        var invalidatorTransaction = new TransactionGate();
        var invalidate = Task.Run(async () =>
        {
            await using var context = database.Context();
            return await CaptureAsync(() => Service(context, tenantId, Guid.NewGuid(), Workflow().Object, Audit().Object,
                    invalidatorTransaction.BlockOnce)
                .RequestTransitionAsync(targetBaseId, new RequestAccountingBookTransitionDto
                {
                    TargetStatus = target.ToString(), Reason = $"request {target}", RowVersion = targetVersion
                }));
        });
        await invalidatorTransaction.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
        // Both serializable service transactions are open here. Commit the child first, then let
        // the invalidator observe the newly attached descendant through its authoritative scan.
        releaseUpdate.TrySetResult();
        var updateOutcome = await update;
        invalidatorTransaction.Release.TrySetResult();
        var invalidateOutcome = await invalidate;
        updateOutcome.Success.Should().BeTrue();
        invalidateOutcome.Success.Should().BeFalse();
        invalidateOutcome.Error.Should().Contain("DELTA_CHILD");

        await using var verify = database.Context();
        var durableBase = await verify.AccountingBooks.AsNoTracking().SingleAsync(item => item.Id == targetBaseId);
        var durableChild = await verify.AccountingBooks.AsNoTracking().SingleAsync(item => item.Id == childId);
        durableBase.LifecycleStatus.Should().Be(targetInitial);
        durableBase.PendingLifecycleStatus.Should().BeNull();
        durableChild.BaseAccountingBookId.Should().Be(targetBaseId);
    }

    private static async Task InvalidationWinsReparentRaceAsync(AccountingBookLifecycleStatus target)
    {
        await using var database = await DisposableDatabase.CreateAsync();
        var tenantId = Guid.NewGuid();
        var primaryId = Guid.NewGuid();
        var safeBaseId = Guid.NewGuid();
        var targetBaseId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var maker = Guid.NewGuid();
        var checker = Guid.NewGuid();
        var targetInitial = target == AccountingBookLifecycleStatus.Suspended
            ? AccountingBookLifecycleStatus.Active
            : AccountingBookLifecycleStatus.Configuring;
        await SeedTenantAsync(database, tenantId,
            Book(primaryId, tenantId, "PRIMARY", AccountingBookType.PrimaryFull,
                AccountingBookLifecycleStatus.Active, isDefault: true),
            Book(safeBaseId, tenantId, "SAFE_BASE", AccountingBookType.ParallelFull,
                AccountingBookLifecycleStatus.Active),
            Book(targetBaseId, tenantId, "TARGET_BASE", AccountingBookType.ParallelFull, targetInitial),
            Book(childId, tenantId, "DELTA_CHILD", AccountingBookType.Delta,
                AccountingBookLifecycleStatus.Draft, baseId: safeBaseId));

        string childVersion;
        string targetVersion;
        await using (var evidence = database.Context())
        {
            childVersion = Convert.ToBase64String((await evidence.AccountingBooks.SingleAsync(item => item.Id == childId)).RowVersion);
            targetVersion = Convert.ToBase64String((await evidence.AccountingBooks.SingleAsync(item => item.Id == targetBaseId)).RowVersion);
        }

        var requestEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRequest = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lifecycleWorkflow = Workflow(beforeStartResult: async () =>
        {
            requestEntered.TrySetResult();
            await releaseRequest.Task;
        });
        var request = Task.Run(async () =>
        {
            await using var requestContext = database.Context();
            return await Service(requestContext, tenantId, maker, lifecycleWorkflow.Object, Audit().Object)
                .RequestTransitionAsync(targetBaseId, new RequestAccountingBookTransitionDto
                {
                    TargetStatus = target.ToString(), Reason = $"request {target}", RowVersion = targetVersion
                });
        });
        await requestEntered.Task.WaitAsync(TimeSpan.FromSeconds(30));

        var childTransaction = new TransactionGate();
        var update = Task.Run(async () =>
        {
            await using var context = database.Context();
            return await CaptureAsync(() => Service(context, tenantId, Guid.NewGuid(), Workflow().Object, Audit().Object,
                    childTransaction.BlockOnce)
                .UpdateAsync(childId, DeltaUpdate("DELTA_CHILD", targetBaseId, childVersion)));
        });
        await childTransaction.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
        // The request writer and child writer now both own open serializable transactions. Commit
        // the pending invalidation first so the child must observe and reject that governed state.
        releaseRequest.TrySetResult();
        var requested = await request;
        childTransaction.Release.TrySetResult();
        var updateOutcome = await update;
        updateOutcome.Success.Should().BeFalse();
        updateOutcome.Error.Should().Match(message =>
            message.Contains(target.ToString(), StringComparison.OrdinalIgnoreCase)
            || message.Contains("pending", StringComparison.OrdinalIgnoreCase));

        await using (var approvalContext = database.Context())
        {
            var approval = await CaptureAsync(() => Service(approvalContext, tenantId, checker, lifecycleWorkflow.Object, Audit().Object)
                .ApproveTransitionAsync(targetBaseId, new DecideAccountingBookTransitionDto
                {
                    Reason = $"approve {target}", RowVersion = requested.RowVersion
                }));
            approval.Success.Should().BeTrue();
        }

        await using var verify = database.Context();
        var durableBase = await verify.AccountingBooks.AsNoTracking().SingleAsync(item => item.Id == targetBaseId);
        var durableChild = await verify.AccountingBooks.AsNoTracking().SingleAsync(item => item.Id == childId);
        durableBase.LifecycleStatus.Should().Be(target);
        durableBase.PendingLifecycleStatus.Should().BeNull();
        durableChild.BaseAccountingBookId.Should().Be(safeBaseId);
    }

    private static async Task SeedTenantAsync(DisposableDatabase database, Guid tenantId, params AccountingBook[] books)
    {
        await using var seed = database.Context();
        await seed.Database.EnsureCreatedAsync();
        seed.Tenants.Add(new Tenant
        {
            Id = tenantId, Code = $"C3{tenantId:N}"[..12].ToUpperInvariant(), Name = "C3 race",
            Status = TenantStatus.Active, BaseCurrency = "GHS"
        });
        seed.FinanceSettings.Add(new FinanceSettings
        {
            Id = Guid.NewGuid(), TenantId = tenantId, BaseCurrency = "GHS"
        });
        seed.AccountingBooks.AddRange(books);
        await seed.SaveChangesAsync();
    }

    private static UpdateAccountingBookDto DeltaUpdate(string code, Guid baseId, string rowVersion) => new()
    {
        Code = code,
        Name = code,
        Purpose = "Adjustment",
        BookType = nameof(AccountingBookType.Delta),
        BaseAccountingBookId = baseId,
        RowVersion = rowVersion
    };

    private static AccountingBook Book(Guid id, Guid tenantId, string code, AccountingBookType type,
        AccountingBookLifecycleStatus status, bool isDefault = false, Guid? baseId = null) => new()
    {
        Id = id, TenantId = tenantId, Code = code, Name = code, Purpose = "Reporting", BookType = type,
        LifecycleStatus = status, FunctionalCurrencyCode = type == AccountingBookType.Delta ? null : "GHS",
        BaseAccountingBookId = baseId, IsDefault = isDefault,
        IsActive = status == AccountingBookLifecycleStatus.Active,
        AllowsPosting = status == AccountingBookLifecycleStatus.Active
    };

    private static AccountingBookService Service(ApplicationDbContext db, Guid tenantId, Guid actor,
        IWorkflowService workflow, IFinanceAuditService audit, Action? afterTransactionOpened = null)
    {
        var current = new Mock<ICurrentUserService>();
        var invoked = 0;
        current.SetupGet(item => item.TenantId).Returns(() =>
        {
            if (Interlocked.Exchange(ref invoked, 1) == 0) afterTransactionOpened?.Invoke();
            return tenantId;
        });
        current.SetupGet(item => item.UserId).Returns(actor.ToString());
        current.SetupGet(item => item.UserName).Returns("finance.c3.sql-race");
        return new AccountingBookService(db, current.Object, workflow, audit);
    }

    private static Mock<IWorkflowService> Workflow(
        Func<Task>? beforeApprovalResult = null,
        Func<Task>? beforeStartResult = null)
    {
        var workflow = new Mock<IWorkflowService>();
        workflow.Setup(item => item.HasActiveApprovalWorkflowAsync(It.IsAny<string>())).ReturnsAsync(true);
        workflow.Setup(item => item.StartApprovalWorkflowAsync(It.IsAny<string>(), It.IsAny<Guid>()))
            .Returns(async () =>
            {
                if (beforeStartResult != null) await beforeStartResult();
                return new WorkflowExecutionResult
                {
                    Success = true, Status = WorkflowInstanceStatus.InProgress, WorkflowInstanceId = Guid.NewGuid()
                };
            });
        workflow.Setup(item => item.CanUserApproveAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>())).ReturnsAsync(true);
        workflow.Setup(item => item.ProcessApprovalStepAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(async () =>
            {
                if (beforeApprovalResult != null) await beforeApprovalResult();
                return new WorkflowExecutionResult { Success = true, Status = WorkflowInstanceStatus.Completed };
            });
        return workflow;
    }

    private static Mock<IFinanceAuditService> Audit(Func<string, Task>? beforeRecord = null)
    {
        var audit = new Mock<IFinanceAuditService>();
        audit.Setup(item => item.RecordAsync(It.IsAny<FinanceAuditEventDto>(), It.IsAny<CancellationToken>()))
            .Returns(async (FinanceAuditEventDto evidence, CancellationToken _) =>
            {
                if (beforeRecord != null) await beforeRecord(evidence.EventType);
                return new AuditLog();
            });
        return audit;
    }

    private sealed class TransactionGate
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void BlockOnce()
        {
            Entered.TrySetResult();
            Release.Task.GetAwaiter().GetResult();
        }
    }

    private sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")))
                Skip = "Set RHEMA_TEST_SQLSERVER to run the prefix-safe disposable C3 concurrency gate.";
        }
    }

    private sealed class DisposableDatabase : IAsyncDisposable
    {
        private static readonly Regex SafeName = new("^RHEMAERP_GL_REHEARSAL_[A-Z0-9_]{1,64}$", RegexOptions.CultureInvariant);
        private readonly string _name;
        private readonly string _masterConnection;
        private readonly DbContextOptions<ApplicationDbContext> _options;

        private DisposableDatabase(string name, string masterConnection, string targetConnection)
        {
            _name = name;
            _masterConnection = masterConnection;
            _options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(targetConnection).Options;
        }

        public static async Task<DisposableDatabase> CreateAsync()
        {
            var configured = Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")
                ?? throw new InvalidOperationException("RHEMA_TEST_SQLSERVER is required.");
            var name = $"RHEMAERP_GL_REHEARSAL_C3RACE_{Guid.NewGuid():N}".ToUpperInvariant();
            AssertSafe(name);
            var master = new SqlConnectionStringBuilder(configured) { InitialCatalog = "master", TrustServerCertificate = true };
            var target = new SqlConnectionStringBuilder(configured) { InitialCatalog = name, TrustServerCertificate = true };
            var database = new DisposableDatabase(name, master.ConnectionString, target.ConnectionString);
            await database.ExecuteMasterAsync($"CREATE DATABASE [{name}]");
            return database;
        }

        public ApplicationDbContext Context() => new(_options);

        public async ValueTask DisposeAsync()
        {
            AssertSafe(_name);
            await ExecuteMasterAsync($"IF DB_ID(N'{_name}') IS NOT NULL BEGIN ALTER DATABASE [{_name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_name}]; END");
        }

        private async Task ExecuteMasterAsync(string sql)
        {
            await using var connection = new SqlConnection(_masterConnection);
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection) { CommandTimeout = 600 };
            await command.ExecuteNonQueryAsync();
        }

        private static void AssertSafe(string name)
        {
            if (!SafeName.IsMatch(name)) throw new InvalidOperationException("Refusing unsafe C3 disposable database target.");
        }
    }
}
