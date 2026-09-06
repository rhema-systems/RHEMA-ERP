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

public sealed class AccountingBookApplicabilityC5MigrationSqlServerTests
{
    [SqlServerFact]
    public async Task Migration_CreatesConstrainedAuthority_RejectsCrossTenantLineage_AndHasBoundedDown()
    {
        await using var database = await DisposableDatabase.CreateAsync();
        var first = await database.CreatePredecessorAsync();
        var second = await database.AddTenantAsync();
        await database.ApplyAsync(up: true);

        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE name LIKE N'AccountingBookApplicability%' OR name LIKE N'AccountingBookSelectionEvidence%'")).Should().Be(5);
        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM sys.triggers WHERE name LIKE N'TR_AccountingBook%C5%'")).Should().Be(5);

        var policyId = Guid.NewGuid();
        var ruleId = Guid.NewGuid();
        await database.InsertDraftPolicyRuleAsync(first.TenantId, policyId, ruleId, "POLICY_A", "RULE_A", 10);
        await database.ExecuteAsync($$"""
INSERT AccountingBookApplicabilityRuleBooks
 (Id,AccountingBookApplicabilityRuleId,AccountingBookId,SelectionOrder,AccountingBookCodeSnapshot,CreatedAt,IsDeleted,TenantId)
VALUES ('{{Guid.NewGuid()}}','{{ruleId}}','{{first.BookId}}',1,N'IFRS',SYSUTCDATETIME(),0,'{{first.TenantId}}');
""");

        var crossTenant = () => database.ExecuteAsync($$"""
INSERT AccountingBookApplicabilityRuleBooks
 (Id,AccountingBookApplicabilityRuleId,AccountingBookId,SelectionOrder,AccountingBookCodeSnapshot,CreatedAt,IsDeleted,TenantId)
VALUES ('{{Guid.NewGuid()}}','{{ruleId}}','{{second.BookId}}',2,N'LOCAL',SYSUTCDATETIME(),0,'{{second.TenantId}}');
""");
        (await crossTenant.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(547);

        var emptyPolicy = Guid.NewGuid();
        await database.InsertDraftPolicyRuleAsync(first.TenantId, emptyPolicy, Guid.NewGuid(), "POLICY_EMPTY", "RULE_EMPTY", 11);
        var approveEmpty = () => database.ExecuteAsync($"UPDATE AccountingBookApplicabilityPolicies SET PolicyStatus=3,ApprovedByUserId='{Guid.NewGuid()}',ApprovedAtUtc=SYSUTCDATETIME() WHERE Id='{emptyPolicy}'");
        (await approveEmpty.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(51000);

        var deltaBook = Guid.NewGuid(); var deltaPolicy = Guid.NewGuid(); var deltaRule = Guid.NewGuid();
        await database.ExecuteAsync($"INSERT AccountingBooks VALUES ('{deltaBook}','{first.TenantId}',N'DELTA',3,4,0);");
        await database.InsertDraftPolicyRuleAsync(first.TenantId, deltaPolicy, deltaRule, "POLICY_DELTA", "RULE_DELTA", 12);
        await database.ExecuteAsync($$"""
INSERT AccountingBookApplicabilityRuleBooks
 (Id,AccountingBookApplicabilityRuleId,AccountingBookId,SelectionOrder,AccountingBookCodeSnapshot,CreatedAt,IsDeleted,TenantId)
VALUES ('{{Guid.NewGuid()}}','{{deltaRule}}','{{deltaBook}}',1,N'DELTA',SYSUTCDATETIME(),0,'{{first.TenantId}}');
""");
        var approveDelta = () => database.ExecuteAsync($"UPDATE AccountingBookApplicabilityPolicies SET PolicyStatus=3,ApprovedByUserId='{Guid.NewGuid()}',ApprovedAtUtc=SYSUTCDATETIME() WHERE Id='{deltaPolicy}'");
        (await approveDelta.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(51000);

        await database.ExecuteAsync($"UPDATE AccountingBookApplicabilityPolicies SET PolicyStatus=3,ApprovedByUserId='{Guid.NewGuid()}',ApprovedAtUtc=SYSUTCDATETIME() WHERE Id='{policyId}'");
        var mutateApprovedRule = () => database.ExecuteAsync($"UPDATE AccountingBookApplicabilityRules SET SortOrder=2 WHERE Id='{ruleId}'");
        (await mutateApprovedRule.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(51000);

        var evidenceId = Guid.NewGuid();
        await database.ExecuteAsync($$"""
INSERT AccountingBookSelectionEvidence
 (Id,AccountingBookApplicabilityPolicyId,AccountingBookApplicabilityRuleId,PolicyVersion,EffectiveDate,OriginatingModuleCode,SourceDocumentType,PostingAction,IdempotencyKey,CalculationInputHash,SelectionFingerprint,FrozenByUserId,FrozenAtUtc,CreatedAt,IsDeleted,TenantId)
VALUES ('{{evidenceId}}','{{policyId}}','{{ruleId}}',1,CONVERT(datetime2,'2026-09-06'),N'FIN',N'JOURNAL',N'POST',N'freeze-1',REPLICATE('A',64),REPLICATE('B',64),'{{Guid.NewGuid()}}',SYSUTCDATETIME(),SYSUTCDATETIME(),0,'{{first.TenantId}}');
INSERT AccountingBookSelectionEvidenceBooks
 (Id,AccountingBookSelectionEvidenceId,AccountingBookId,SelectionOrder,AccountingBookCodeSnapshot,AuthorityFingerprint,CreatedAt,IsDeleted,TenantId)
VALUES ('{{Guid.NewGuid()}}','{{evidenceId}}','{{first.BookId}}',0,N'IFRS',REPLICATE('C',64),SYSUTCDATETIME(),0,'{{first.TenantId}}');
""");
        var mutateFrozen = () => database.ExecuteAsync($"UPDATE AccountingBookSelectionEvidence SET PostingAction=N'REVERSE' WHERE Id='{evidenceId}'");
        (await mutateFrozen.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(51000);

        var lossyDown = () => database.ApplyAsync(up: false);
        (await lossyDown.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(51000);

        await using var empty = await DisposableDatabase.CreateAsync();
        await empty.CreatePredecessorAsync();
        await empty.ApplyAsync(up: true);
        await empty.ApplyAsync(up: false);
        (await empty.ScalarAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE name LIKE N'AccountingBookApplicability%' OR name LIKE N'AccountingBookSelectionEvidence%'"))
            .Should().Be(0);
        (await empty.ScalarAsync<int>("SELECT COUNT(*) FROM sys.key_constraints WHERE name=N'AK_AccountingBooks_TenantId_Id_Code'"))
            .Should().Be(0);
    }

    [SqlServerFact]
    public async Task Migration_RejectsEqualPriorityApprovedAmbiguity_UnderConcurrentTransactions()
    {
        await using var database = await DisposableDatabase.CreateAsync();
        var authority = await database.CreatePredecessorAsync();
        await database.ApplyAsync(up: true);

        var firstPolicy = Guid.NewGuid();
        var secondPolicy = Guid.NewGuid();
        await database.InsertDraftPolicyRuleAsync(authority.TenantId, firstPolicy, Guid.NewGuid(), "POLICY_A", "RULE_A", 10);
        await database.InsertDraftPolicyRuleAsync(authority.TenantId, secondPolicy, Guid.NewGuid(), "POLICY_B", "RULE_B", 10);

        await using var firstConnection = await database.OpenAsync();
        await using var secondConnection = await database.OpenAsync();
        await using var firstTransaction = (SqlTransaction)await firstConnection.BeginTransactionAsync();
        await using var secondTransaction = (SqlTransaction)await secondConnection.BeginTransactionAsync();

        await database.ApproveAsync(firstConnection, firstTransaction, firstPolicy);
        var competingApproval = database.ApproveAsync(secondConnection, secondTransaction, secondPolicy);
        await Task.Yield();
        await firstTransaction.CommitAsync();

        Func<Task> observeCompetingApproval = async () => await competingApproval;
        var failure = await observeCompetingApproval.Should().ThrowAsync<SqlException>();
        failure.Which.Number.Should().BeOneOf(51000, 1205);
        await secondTransaction.RollbackAsync();
        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM AccountingBookApplicabilityPolicies WHERE PolicyStatus=3"))
            .Should().Be(1);
    }

    [SqlServerFact]
    public async Task Migration_SerializesConcurrentSameLineageSuccessorApprovals()
    {
        await using var database = await DisposableDatabase.CreateAsync();
        var authority = await database.CreatePredecessorAsync(); await database.ApplyAsync(up: true);
        var predecessor = Guid.NewGuid(); var predecessorRule = Guid.NewGuid();
        await database.InsertDraftPolicyRuleAsync(authority.TenantId, predecessor, predecessorRule, "REPLACE", "RULE_V1", 10);
        await database.InsertRuleBookAsync(authority.TenantId, predecessorRule, authority.BookId, "IFRS");
        await database.ExecuteAsync($"UPDATE AccountingBookApplicabilityPolicies SET PolicyStatus=3,ApprovedByUserId='{Guid.NewGuid()}',ApprovedAtUtc=SYSUTCDATETIME() WHERE Id='{predecessor}'");

        var second = Guid.NewGuid(); var secondRule = Guid.NewGuid(); var third = Guid.NewGuid(); var thirdRule = Guid.NewGuid();
        await database.InsertDraftPolicyRuleAsync(authority.TenantId, second, secondRule, "REPLACE", "RULE_V2", 10, 2, predecessor, new DateTime(2026, 9, 6));
        await database.InsertRuleBookAsync(authority.TenantId, secondRule, authority.BookId, "IFRS");
        await database.InsertDraftPolicyRuleAsync(authority.TenantId, third, thirdRule, "REPLACE", "RULE_V3", 10, 3, second, new DateTime(2026, 9, 6));
        await database.InsertRuleBookAsync(authority.TenantId, thirdRule, authority.BookId, "IFRS");

        await using var firstConnection = await database.OpenAsync(); await using var secondConnection = await database.OpenAsync();
        await using var firstTransaction = (SqlTransaction)await firstConnection.BeginTransactionAsync();
        await using var secondTransaction = (SqlTransaction)await secondConnection.BeginTransactionAsync();
        await database.ApproveAsync(firstConnection, firstTransaction, second);
        var competing = database.ApproveAsync(secondConnection, secondTransaction, third); await Task.Yield(); await firstTransaction.CommitAsync();
        Func<Task> observe = async () => await competing;
        (await observe.Should().ThrowAsync<SqlException>()).Which.Number.Should().BeOneOf(51000, 1205);
        await secondTransaction.RollbackAsync();
        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM AccountingBookApplicabilityPolicies WHERE PolicyCode=N'REPLACE' AND PolicyStatus=3"))
            .Should().Be(2);
    }

    [SqlServerFact]
    public async Task Migration_FailsBeforeMutation_WhenC5SchemaCollides()
    {
        await using var database = await DisposableDatabase.CreateAsync();
        await database.CreatePredecessorAsync();
        await database.ExecuteAsync("CREATE TABLE AccountingBookApplicabilityPolicies (Id uniqueidentifier NOT NULL PRIMARY KEY);");

        var apply = () => database.ApplyAsync(up: true);
        (await apply.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(51000);
        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE name IN (N'AccountingBookApplicabilityRules',N'AccountingBookApplicabilityRuleBooks',N'AccountingBookSelectionEvidence',N'AccountingBookSelectionEvidenceBooks')"))
            .Should().Be(0);
        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM sys.key_constraints WHERE name=N'AK_AccountingBooks_TenantId_Id_Code'"))
            .Should().Be(0);
    }

    [SqlServerFact]
    public async Task Migration_FailsBeforeMutation_ForNoncanonicalPseudoAndOrphanBookAuthority()
    {
        foreach (var corruption in new[]
        {
            "UPDATE AccountingBooks SET Code=N'ifrs'",
            "UPDATE AccountingBooks SET Code=N'ALL_ACTIVE_BOOKS'",
            "UPDATE AccountingBooks SET TenantId=NEWID()"
        })
        {
            await using var database = await DisposableDatabase.CreateAsync();
            await database.CreatePredecessorAsync(); await database.ExecuteAsync(corruption);
            var apply = () => database.ApplyAsync(up: true);
            (await apply.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(51000);
            (await database.ScalarAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE name LIKE N'AccountingBookApplicability%' OR name LIKE N'AccountingBookSelectionEvidence%'"))
                .Should().Be(0);
            (await database.ScalarAsync<int>("SELECT COUNT(*) FROM sys.key_constraints WHERE name=N'AK_AccountingBooks_TenantId_Id_Code'"))
                .Should().Be(0);
        }
    }

    private sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")))
                Skip = "Set RHEMA_TEST_SQLSERVER to run prefix-safe disposable C5 migration gates.";
        }
    }

    private sealed class DisposableDatabase : IAsyncDisposable
    {
        private readonly string _name;
        private readonly string _master;
        private readonly string _connection;

        private DisposableDatabase(string name, string master, string connection) =>
            (_name, _master, _connection) = (name, master, connection);

        public static async Task<DisposableDatabase> CreateAsync()
        {
            var configured = Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")
                ?? throw new InvalidOperationException("RHEMA_TEST_SQLSERVER is required.");
            var name = $"RHEMAERP_GL_REHEARSAL_C5_{Guid.NewGuid():N}";
            if (!name.StartsWith("RHEMAERP_GL_REHEARSAL_C5_", StringComparison.Ordinal))
                throw new InvalidOperationException("Disposable database prefix assertion failed.");
            var master = new SqlConnectionStringBuilder(configured) { InitialCatalog = "master", TrustServerCertificate = true };
            var target = new SqlConnectionStringBuilder(configured) { InitialCatalog = name, TrustServerCertificate = true };
            var database = new DisposableDatabase(name, master.ConnectionString, target.ConnectionString);
            await database.ExecuteMasterAsync($"CREATE DATABASE [{name}]");
            return database;
        }

        public async Task<(Guid TenantId, Guid BookId)> CreatePredecessorAsync()
        {
            var tenantId = Guid.NewGuid();
            var bookId = Guid.NewGuid();
            await ExecuteAsync($$"""
CREATE TABLE Tenants (Id uniqueidentifier NOT NULL CONSTRAINT PK_Tenants PRIMARY KEY, IsDeleted bit NOT NULL);
CREATE TABLE AccountingBooks (
 Id uniqueidentifier NOT NULL CONSTRAINT PK_AccountingBooks PRIMARY KEY, TenantId uniqueidentifier NOT NULL,
 Code nvarchar(20) NOT NULL, BookType int NOT NULL, LifecycleStatus int NOT NULL, IsDeleted bit NOT NULL);
INSERT Tenants VALUES ('{{tenantId}}',0);
INSERT AccountingBooks VALUES ('{{bookId}}','{{tenantId}}',N'IFRS',1,4,0);
""");
            return (tenantId, bookId);
        }

        public async Task<(Guid TenantId, Guid BookId)> AddTenantAsync()
        {
            var tenantId = Guid.NewGuid();
            var bookId = Guid.NewGuid();
            await ExecuteAsync($$"""
INSERT Tenants VALUES ('{{tenantId}}',0);
INSERT AccountingBooks VALUES ('{{bookId}}','{{tenantId}}',N'LOCAL',2,4,0);
""");
            return (tenantId, bookId);
        }

        public Task InsertDraftPolicyRuleAsync(Guid tenantId, Guid policyId, Guid ruleId, string policyCode, string ruleCode, int priority,
            int version = 1, Guid? supersedes = null, DateTime? effectiveFrom = null) =>
            ExecuteAsync($$"""
INSERT AccountingBookApplicabilityPolicies
 (Id,PolicyCode,Version,SupersedesPolicyId,Name,EffectiveFrom,PolicyStatus,Reason,CreatedByUserId,PreparedByUserId,PreparedAtUtc,CreatedAt,IsDeleted,TenantId)
VALUES ('{{policyId}}',N'{{policyCode}}',{{version}},{{(supersedes.HasValue ? $"'{supersedes.Value}'" : "NULL")}},N'Policy',CONVERT(datetime2,'{{(effectiveFrom ?? new DateTime(2026, 1, 1)):yyyy-MM-dd}}'),1,N'reason','{{Guid.NewGuid()}}','{{Guid.NewGuid()}}',SYSUTCDATETIME(),SYSUTCDATETIME(),0,'{{tenantId}}');
INSERT AccountingBookApplicabilityRules
 (Id,AccountingBookApplicabilityPolicyId,RuleCode,Priority,OriginatingModuleCode,SourceDocumentType,PostingAction,SortOrder,CreatedAt,IsDeleted,TenantId)
VALUES ('{{ruleId}}','{{policyId}}',N'{{ruleCode}}',{{priority}},N'FIN',N'JOURNAL',N'POST',1,SYSUTCDATETIME(),0,'{{tenantId}}');
""");

        public Task InsertRuleBookAsync(Guid tenantId, Guid ruleId, Guid bookId, string code) => ExecuteAsync($$"""
INSERT AccountingBookApplicabilityRuleBooks
 (Id,AccountingBookApplicabilityRuleId,AccountingBookId,SelectionOrder,AccountingBookCodeSnapshot,CreatedAt,IsDeleted,TenantId)
VALUES ('{{Guid.NewGuid()}}','{{ruleId}}','{{bookId}}',0,N'{{code}}',SYSUTCDATETIME(),0,'{{tenantId}}');
""");

        public async Task ApproveAsync(SqlConnection connection, SqlTransaction transaction, Guid policyId)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"UPDATE AccountingBookApplicabilityPolicies SET PolicyStatus=3,ApprovedByUserId='{Guid.NewGuid()}',ApprovedAtUtc=SYSUTCDATETIME() WHERE Id='{policyId}'";
            await command.ExecuteNonQueryAsync();
        }

        public async Task ApplyAsync(bool up)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(_connection).Options;
            await using var context = new ApplicationDbContext(options);
            var generator = context.GetService<IMigrationsSqlGenerator>();
            var migration = new ExposedMigration();
            var operations = up ? migration.BuildUpOperations() : migration.BuildDownOperations();
            foreach (var command in generator.Generate(operations))
                await ExecuteAsync(command.CommandText);
        }

        public async Task<SqlConnection> OpenAsync()
        {
            var connection = new SqlConnection(_connection);
            await connection.OpenAsync();
            return connection;
        }

        public async Task ExecuteAsync(string sql)
        {
            await using var connection = await OpenAsync();
            await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
            await command.ExecuteNonQueryAsync();
        }

        public async Task<T> ScalarAsync<T>(string sql)
        {
            await using var connection = await OpenAsync();
            await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
            return (T)Convert.ChangeType(await command.ExecuteScalarAsync(), typeof(T));
        }

        private async Task ExecuteMasterAsync(string sql)
        {
            await using var connection = new SqlConnection(_master);
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
            await command.ExecuteNonQueryAsync();
        }

        public async ValueTask DisposeAsync()
        {
            if (!_name.StartsWith("RHEMAERP_GL_REHEARSAL_C5_", StringComparison.Ordinal)) return;
            await ExecuteMasterAsync($"IF DB_ID(N'{_name}') IS NOT NULL BEGIN ALTER DATABASE [{_name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_name}]; END");
        }

        private sealed class ExposedMigration : AddAccountingBookApplicabilityFoundation
        {
            public IReadOnlyList<MigrationOperation> BuildUpOperations()
            {
                var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
                Up(builder);
                return builder.Operations;
            }

            public IReadOnlyList<MigrationOperation> BuildDownOperations()
            {
                var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
                Down(builder);
                return builder.Operations;
            }
        }
    }
}
