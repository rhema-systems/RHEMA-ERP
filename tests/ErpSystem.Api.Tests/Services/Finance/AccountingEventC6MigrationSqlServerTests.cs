using System.Text.RegularExpressions;
using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class AccountingEventC6MigrationSqlServerTests
{
    [SqlServerFact]
    public async Task UpAndEmptyDownCreateAndRemoveOnlyC6Authority()
    {
        await using var db = await DisposableDatabase.CreateAsync();
        await db.CreatePredecessorAsync();
        await db.ApplyAsync(up: true);
        (await db.ScalarAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE name IN ('AccountingEvents','AccountingEventPostings','AccountingEventAttempts')")).Should().Be(3);
        (await db.ScalarAsync<int>("SELECT COUNT(*) FROM sys.triggers WHERE name LIKE 'TR_AccountingEvent%_C6%' OR name='TR_AccountingEventAttempts_C6AppendOnly'")).Should().Be(3);
        await db.ApplyAsync(up: false);
        (await db.ScalarAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE name LIKE 'AccountingEvent%'")).Should().Be(0);
    }

    [SqlServerFact]
    public async Task PreflightFailureOccursBeforeUniqueConstraintsOrC6Tables()
    {
        await using var db = await DisposableDatabase.CreateAsync();
        await db.CreatePredecessorAsync();
        await db.ExecuteAsync("CREATE TABLE AccountingEvents(Id uniqueidentifier NOT NULL PRIMARY KEY);");
        await FluentActions.Awaiting(() => db.ApplyAsync(up: true)).Should().ThrowAsync<SqlException>().WithMessage("*C6_SCHEMA_PREFLIGHT*");
        (await db.ScalarAsync<int>("SELECT COUNT(*) FROM sys.key_constraints WHERE name IN ('AK_JournalEntries_TenantId_Id','AK_FinancePostingEvents_TenantId_Id')")).Should().Be(0);
        (await db.ScalarAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE name IN ('AccountingEventPostings','AccountingEventAttempts')")).Should().Be(0);
    }

    [SqlServerFact]
    public async Task FailedEventAndAttemptPersistButBoundedDownRefusesTheirLoss()
    {
        await using var db = await DisposableDatabase.CreateAsync();
        await db.CreatePredecessorAsync();
        await db.ApplyAsync(up: true);
        var tenant = Guid.NewGuid(); var eventId = Guid.NewGuid(); var actor = Guid.NewGuid();
        await db.ExecuteAsync($"INSERT Tenants(Id) VALUES('{tenant}');" + EventInsert(tenant, eventId, actor, "FAILURE-KEY", "FAILED", status: "Failed") +
            $"INSERT AccountingEventAttempts(Id,AccountingEventId,AttemptNumber,RequestFingerprint,Status,StartedAtUtc,CompletedAtUtc,FailureMessage,CreatedAt,IsDeleted,TenantId) VALUES('{Guid.NewGuid()}','{eventId}',1,REPLICATE('A',64),'Failed',SYSUTCDATETIME(),SYSUTCDATETIME(),N'leaf failed',SYSUTCDATETIME(),0,'{tenant}');");
        (await db.ScalarAsync<int>("SELECT COUNT(*) FROM AccountingEvents WHERE Status='Failed'")).Should().Be(1);
        (await db.ScalarAsync<int>("SELECT COUNT(*) FROM AccountingEventAttempts WHERE Status='Failed'")).Should().Be(1);
        await FluentActions.Awaiting(() => db.ApplyAsync(up: false)).Should().ThrowAsync<SqlException>().WithMessage("*C6_DOWN_BLOCKED*");
        (await db.ScalarAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE name='AccountingEvents'")).Should().Be(1);
    }

    [SqlServerFact]
    public async Task LaterDatedSuccessorReusesExactPostedPredecessorSelectionEvidence()
    {
        await using var db = await DisposableDatabase.CreateAsync();
        await db.CreatePredecessorAsync();
        await db.ApplyAsync(up: true);
        var tenant = Guid.NewGuid(); var maker = Guid.NewGuid(); var checker = Guid.NewGuid();
        var book = Guid.NewGuid(); var evidence = Guid.NewGuid(); var source = Guid.NewGuid();
        var original = Guid.NewGuid(); var successor = Guid.NewGuid(); var journal = Guid.NewGuid(); var leaf = Guid.NewGuid();
        await db.ExecuteAsync($@"
INSERT Tenants(Id) VALUES('{tenant}');
INSERT AccountingBooks(Id,TenantId,Code) VALUES('{book}','{tenant}',N'PRIMARY');
INSERT AccountingBookSelectionEvidence(Id,TenantId,OriginatingModuleCode,SourceDocumentType,PostingAction,EffectiveDate,IdempotencyKey,SelectionFingerprint,IsDeleted)
 VALUES('{evidence}','{tenant}',N'FIN',N'JOURNAL',N'POST',CONVERT(date,'2026-09-07'),N'SELECTION-1',REPLICATE('B',64),0);
INSERT AccountingBookSelectionEvidenceBooks(Id,TenantId,AccountingBookSelectionEvidenceId,AccountingBookId,SelectionOrder,AccountingBookCodeSnapshot,AuthorityFingerprint,IsDeleted)
 VALUES('{Guid.NewGuid()}','{tenant}','{evidence}','{book}',0,N'PRIMARY',REPLICATE('C',64),0);
INSERT AccountingEvents(Id,OriginatingModuleCode,SourceDocumentType,SourceDocumentId,PostingAction,IdempotencyKey,EventKind,Version,RootAccountingEventId,
 SelectionFingerprint,RequestFingerprint,Status,EventDate,RequestedAtUtc,RequestedByUserId,PreparedByUserId,PreparedAtUtc,CreatedAt,IsDeleted,TenantId)
 VALUES('{original}',N'FIN',N'JOURNAL','{source}',N'POST',N'ORIGINAL-1',N'Original',1,'{original}',REPLICATE('B',64),REPLICATE('A',64),N'PendingApproval',
 CONVERT(date,'2026-09-07'),SYSUTCDATETIME(),'{maker}','{maker}',SYSUTCDATETIME(),SYSUTCDATETIME(),0,'{tenant}');
UPDATE AccountingEvents SET AccountingBookSelectionEvidenceId='{evidence}',Status=N'Pending',ReleasedByUserId='{checker}',ReleasedAtUtc=SYSUTCDATETIME(),ReleaseReason=N'release'
 WHERE Id='{original}';
INSERT JournalEntries(Id,TenantId,AccountingBookId,PostingStatus) VALUES('{journal}','{tenant}','{book}',N'Posted');
INSERT FinancePostingEvents(Id,TenantId,AccountingBookId,JournalEntryId,PostingStatus) VALUES('{leaf}','{tenant}','{book}','{journal}',N'Posted');
INSERT AccountingEventPostings(Id,AccountingEventId,EventVersion,AccountingBookId,SelectionOrder,AccountingBookCodeSnapshot,AuthorityFingerprint,Status,
 FinancePostingEventId,JournalEntryId,PostedAtUtc,CreatedAt,IsDeleted,TenantId)
 VALUES('{Guid.NewGuid()}','{original}',1,'{book}',0,N'PRIMARY',REPLICATE('C',64),N'Posted','{leaf}','{journal}',SYSUTCDATETIME(),SYSUTCDATETIME(),0,'{tenant}');
UPDATE AccountingEvents SET Status=N'Posted',CompletedAtUtc=SYSUTCDATETIME() WHERE Id='{original}';
INSERT AccountingEvents(Id,OriginatingModuleCode,SourceDocumentType,SourceDocumentId,PostingAction,IdempotencyKey,EventKind,Version,RootAccountingEventId,
 SupersedesAccountingEventId,ReversesAccountingEventId,SelectionFingerprint,RequestFingerprint,Status,EventDate,RequestedAtUtc,RequestedByUserId,
 PreparedByUserId,PreparedAtUtc,CreatedAt,IsDeleted,TenantId)
 VALUES('{successor}',N'FIN',N'JOURNAL','{source}',N'POST',N'REVERSAL-1',N'Reversal',2,'{original}','{original}','{original}',REPLICATE('B',64),REPLICATE('D',64),
 N'PendingApproval',CONVERT(date,'2026-09-08'),SYSUTCDATETIME(),'{maker}','{maker}',SYSUTCDATETIME(),SYSUTCDATETIME(),0,'{tenant}');
UPDATE AccountingEvents SET AccountingBookSelectionEvidenceId='{evidence}',Status=N'Pending',ReleasedByUserId='{checker}',ReleasedAtUtc=SYSUTCDATETIME(),ReleaseReason=N'reversal'
 WHERE Id='{successor}';");
        (await db.ScalarAsync<int>($"SELECT COUNT(*) FROM AccountingEvents WHERE Id='{successor}' AND Status='Pending' AND EventDate=CONVERT(date,'2026-09-08') AND AccountingBookSelectionEvidenceId='{evidence}'"))
            .Should().Be(1);
    }

    [SqlServerFact]
    public async Task ConcurrentSameTenantIdempotencyPersistsExactlyOneEvent()
    {
        await using var db = await DisposableDatabase.CreateAsync();
        await db.CreatePredecessorAsync();
        await db.ApplyAsync(up: true);
        var tenant = Guid.NewGuid(); var actor = Guid.NewGuid();
        await db.ExecuteAsync($"INSERT Tenants(Id) VALUES('{tenant}');");
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var arrivals = 0;
        async Task<Exception?> InsertAsync(string action)
        {
            await using var connection = await db.OpenAsync();
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
            if (Interlocked.Increment(ref arrivals) == 2) ready.TrySetResult();
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(30));
            try
            {
                await using var command = connection.CreateCommand(); command.Transaction = transaction;
                command.CommandText = EventInsert(tenant, Guid.NewGuid(), actor, "RACE-KEY", action, "PendingApproval");
                await command.ExecuteNonQueryAsync(); await transaction.CommitAsync(); return null;
            }
            catch (Exception ex) { try { await transaction.RollbackAsync(); } catch { } return ex; }
        }
        var outcomes = await Task.WhenAll(Task.Run(() => InsertAsync("POST-A")), Task.Run(() => InsertAsync("POST-B")));
        outcomes.Count(item => item is null).Should().Be(1);
        outcomes.Count(item => item is SqlException).Should().Be(1);
        (await db.ScalarAsync<int>("SELECT COUNT(*) FROM AccountingEvents WHERE TenantId='" + tenant + "' AND IdempotencyKey='RACE-KEY'")).Should().Be(1);
    }

    private static string EventInsert(Guid tenant, Guid id, Guid actor, string key, string action, string status) => $@"
INSERT AccountingEvents(Id,OriginatingModuleCode,SourceDocumentType,SourceDocumentId,PostingAction,IdempotencyKey,EventKind,Version,RootAccountingEventId,
 SelectionFingerprint,RequestFingerprint,Status,EventDate,RequestedAtUtc,RequestedByUserId,PreparedByUserId,PreparedAtUtc,
 ReleasedByUserId,ReleasedAtUtc,ReleaseReason,CompletedAtUtc,FailureMessage,CreatedAt,IsDeleted,TenantId)
VALUES('{id}',N'FIN',N'JOURNAL','{Guid.NewGuid()}',N'{action}',N'{key}',N'Original',1,'{id}',N'',REPLICATE('A',64),N'{status}',CONVERT(date,'2026-09-07'),
 SYSUTCDATETIME(),'{actor}','{actor}',SYSUTCDATETIME(),{(status == "PendingApproval" ? "NULL,NULL,NULL,NULL,NULL" : $"'{Guid.NewGuid()}',SYSUTCDATETIME(),N'release',SYSUTCDATETIME(),N'leaf failed'")},SYSUTCDATETIME(),0,'{tenant}');";

    private sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")))
                Skip = "Set RHEMA_TEST_SQLSERVER to run exact-prefix disposable C6 migration gates.";
        }
    }

    private sealed class DisposableDatabase : IAsyncDisposable
    {
        private static readonly Regex SafeName = new("^RHEMAERP_GL_REHEARSAL_C6_[A-Z0-9_]{1,64}$", RegexOptions.CultureInvariant);
        private readonly string _name; private readonly string _master; private readonly string _connection;
        private DisposableDatabase(string name, string master, string connection) => (_name, _master, _connection) = (name, master, connection);

        public static async Task<DisposableDatabase> CreateAsync()
        {
            var configured = Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER") ?? throw new InvalidOperationException("RHEMA_TEST_SQLSERVER is required.");
            var name = $"RHEMAERP_GL_REHEARSAL_C6_{Guid.NewGuid():N}".ToUpperInvariant();
            if (!SafeName.IsMatch(name)) throw new InvalidOperationException("Disposable C6 database prefix assertion failed.");
            var master = new SqlConnectionStringBuilder(configured) { InitialCatalog = "master", TrustServerCertificate = true };
            var target = new SqlConnectionStringBuilder(configured) { InitialCatalog = name, TrustServerCertificate = true };
            var result = new DisposableDatabase(name, master.ConnectionString, target.ConnectionString);
            await result.MasterAsync($"CREATE DATABASE [{name}]"); return result;
        }

        public Task CreatePredecessorAsync() => ExecuteAsync(@"
CREATE TABLE Tenants(Id uniqueidentifier NOT NULL CONSTRAINT PK_Tenants PRIMARY KEY);
CREATE TABLE AccountingBooks(Id uniqueidentifier NOT NULL CONSTRAINT PK_AccountingBooks PRIMARY KEY,TenantId uniqueidentifier NOT NULL,Code nvarchar(20) NOT NULL,
 CONSTRAINT AK_AccountingBooks_TenantId_Id UNIQUE(TenantId,Id));
CREATE TABLE AccountingBookSelectionEvidence(Id uniqueidentifier NOT NULL CONSTRAINT PK_AccountingBookSelectionEvidence PRIMARY KEY,TenantId uniqueidentifier NOT NULL,
 OriginatingModuleCode nvarchar(40) NOT NULL,SourceDocumentType nvarchar(80) NOT NULL,PostingAction nvarchar(60) NOT NULL,EffectiveDate datetime2 NOT NULL,
 IdempotencyKey nvarchar(100) NOT NULL,SelectionFingerprint nvarchar(64) NOT NULL,IsDeleted bit NOT NULL,
 CONSTRAINT AK_AccountingBookSelectionEvidence_TenantId_Id UNIQUE(TenantId,Id));
CREATE TABLE AccountingBookSelectionEvidenceBooks(Id uniqueidentifier NOT NULL CONSTRAINT PK_AccountingBookSelectionEvidenceBooks PRIMARY KEY,TenantId uniqueidentifier NOT NULL,
 AccountingBookSelectionEvidenceId uniqueidentifier NOT NULL,AccountingBookId uniqueidentifier NOT NULL,SelectionOrder int NOT NULL,
 AccountingBookCodeSnapshot nvarchar(20) NOT NULL,AuthorityFingerprint nvarchar(64) NOT NULL,IsDeleted bit NOT NULL);
CREATE TABLE JournalEntries(Id uniqueidentifier NOT NULL CONSTRAINT PK_JournalEntries PRIMARY KEY,TenantId uniqueidentifier NOT NULL,AccountingBookId uniqueidentifier NOT NULL,PostingStatus nvarchar(20) NOT NULL);
CREATE TABLE FinancePostingEvents(Id uniqueidentifier NOT NULL CONSTRAINT PK_FinancePostingEvents PRIMARY KEY,TenantId uniqueidentifier NOT NULL,AccountingBookId uniqueidentifier NOT NULL,
 JournalEntryId uniqueidentifier NULL,PostingStatus nvarchar(20) NOT NULL);");

        public async Task ApplyAsync(bool up)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(_connection).Options;
            await using var context = new ApplicationDbContext(options);
            var generator = context.GetService<IMigrationsSqlGenerator>(); var migration = new ExposedMigration();
            foreach (var command in generator.Generate(up ? migration.UpOperations() : migration.DownOperations())) await ExecuteAsync(command.CommandText);
        }
        public async Task<SqlConnection> OpenAsync() { var connection = new SqlConnection(_connection); await connection.OpenAsync(); return connection; }
        public async Task ExecuteAsync(string sql) { await using var connection = await OpenAsync(); await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 }; await command.ExecuteNonQueryAsync(); }
        public async Task<T> ScalarAsync<T>(string sql) { await using var connection = await OpenAsync(); await using var command = new SqlCommand(sql, connection); return (T)Convert.ChangeType(await command.ExecuteScalarAsync(), typeof(T)); }
        private async Task MasterAsync(string sql) { await using var connection = new SqlConnection(_master); await connection.OpenAsync(); await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 }; await command.ExecuteNonQueryAsync(); }
        public async ValueTask DisposeAsync() { if (SafeName.IsMatch(_name)) await MasterAsync($"IF DB_ID(N'{_name}') IS NOT NULL BEGIN ALTER DATABASE [{_name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_name}]; END"); }

        private sealed class ExposedMigration : AddAccountingEventOrchestrationFoundation
        {
            public IReadOnlyList<MigrationOperation> UpOperations() { var b = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer"); Up(b); return b.Operations; }
            public IReadOnlyList<MigrationOperation> DownOperations() { var b = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer"); Down(b); return b.Operations; }
        }
    }
}
