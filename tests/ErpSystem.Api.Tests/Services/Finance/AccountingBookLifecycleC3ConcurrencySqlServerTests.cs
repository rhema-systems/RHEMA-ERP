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

        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var approve = Task.Run(async () =>
        {
            await start.Task;
            await using var context = database.Context();
            return await CaptureAsync(() => Service(context, tenantId, checker, workflow.Object, audit.Object)
                .ApproveTransitionAsync(leafId, new DecideAccountingBookTransitionDto
                {
                    Reason = "approve initialization", RowVersion = leafRowVersion
                }));
        });
        var suspend = Task.Run(async () =>
        {
            await start.Task;
            await using var context = database.Context();
            return await CaptureAsync(() => Service(context, tenantId, Guid.NewGuid(), workflow.Object, audit.Object)
                .RequestTransitionAsync(rootId, new RequestAccountingBookTransitionDto
                {
                    TargetStatus = "Suspended", Reason = "suspend root", RowVersion = rootRowVersion
                }));
        });
        start.SetResult();
        var outcomes = await Task.WhenAll(approve, suspend);

        outcomes.Should().ContainSingle(item => item.Success);
        outcomes.Should().ContainSingle(item => !item.Success && item.Error!.Contains("DELTA_LEAF", StringComparison.Ordinal));
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
        IWorkflowService workflow, IFinanceAuditService audit)
    {
        var current = new Mock<ICurrentUserService>();
        current.SetupGet(item => item.TenantId).Returns(tenantId);
        current.SetupGet(item => item.UserId).Returns(actor.ToString());
        current.SetupGet(item => item.UserName).Returns("finance.c3.sql-race");
        return new AccountingBookService(db, current.Object, workflow, audit);
    }

    private static Mock<IWorkflowService> Workflow()
    {
        var workflow = new Mock<IWorkflowService>();
        workflow.Setup(item => item.HasActiveApprovalWorkflowAsync(It.IsAny<string>())).ReturnsAsync(true);
        workflow.Setup(item => item.StartApprovalWorkflowAsync(It.IsAny<string>(), It.IsAny<Guid>()))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true, Status = WorkflowInstanceStatus.InProgress, WorkflowInstanceId = Guid.NewGuid()
            });
        workflow.Setup(item => item.CanUserApproveAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>())).ReturnsAsync(true);
        workflow.Setup(item => item.ProcessApprovalStepAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(new WorkflowExecutionResult { Success = true, Status = WorkflowInstanceStatus.Completed });
        return workflow;
    }

    private static Mock<IFinanceAuditService> Audit()
    {
        var audit = new Mock<IFinanceAuditService>();
        audit.Setup(item => item.RecordAsync(It.IsAny<FinanceAuditEventDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuditLog());
        return audit;
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
