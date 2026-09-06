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

public sealed class AccountingBookPeriodInitializationC4MigrationSqlServerTests
{
    [SqlServerFact]
    public async Task Migration_CreatesConstrainedAuthority_RejectsLossyDown_AndRestoresPredecessor()
    {
        await using var database = await DisposableDatabase.CreateAsync();
        var evidence = await database.CreatePredecessorAsync();

        await database.ApplyAsync(up: true);

        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE name IN (N'AccountingBookPeriods',N'AccountingBookInitializations',N'AccountingBookInitializationLines')"))
            .Should().Be(3);
        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM sys.foreign_keys WHERE name=N'FK_AccountingBookPeriods_FiscalPeriods_TenantId_FiscalPeriodId'"))
            .Should().Be(1);
        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM sys.foreign_keys WHERE name=N'FK_AccountingBookInitializationLines_Accounts_TenantId_AccountId'"))
            .Should().Be(1);

        var initializationId = Guid.NewGuid();
        await database.ExecuteAsync($$"""
INSERT AccountingBookPeriods (Id,AccountingBookId,FiscalPeriodId,PeriodStatus,CreatedAt,IsDeleted,TenantId)
VALUES ('{{Guid.NewGuid()}}','{{evidence.BookId}}','{{evidence.PeriodId}}',1,SYSUTCDATETIME(),0,'{{evidence.TenantId}}');
INSERT AccountingBookInitializations
 (Id,AccountingBookId,Version,Mode,InitializationStatus,CutoffDate,CutoffFiscalPeriodId,IdempotencyKey,Reason,TotalDebits,TotalCredits,
  RequiredAccountCount,CoveredAccountCount,EvidenceFingerprint,ReconciliationFingerprint,PreparedByUserId,PreparedAtUtc,CreatedAt,IsDeleted,TenantId)
VALUES
 ('{{initializationId}}','{{evidence.BookId}}',1,1,1,SYSUTCDATETIME(),'{{evidence.PeriodId}}',N'c4-sql-success',N'rehearsal',0,0,
  1,1,REPLICATE(N'A',64),REPLICATE(N'B',64),'{{Guid.NewGuid()}}',SYSUTCDATETIME(),SYSUTCDATETIME(),0,'{{evidence.TenantId}}');
INSERT AccountingBookInitializationLines
 (Id,AccountingBookInitializationId,AccountId,CurrencyCode,OpeningDebit,OpeningCredit,BaseBookSignedBalance,OpeningAdjustment,CreatedAt,IsDeleted,TenantId)
VALUES
 ('{{Guid.NewGuid()}}','{{initializationId}}','{{evidence.AccountId}}',N'GHS',0,0,0,0,SYSUTCDATETIME(),0,'{{evidence.TenantId}}');
""");

        await database.ExecuteAsync("DELETE AccountingBookInitializationLines;");
        var invalidCurrency = () => database.ExecuteAsync($$"""
INSERT AccountingBookInitializationLines
 (Id,AccountingBookInitializationId,AccountId,CurrencyCode,OpeningDebit,OpeningCredit,BaseBookSignedBalance,OpeningAdjustment,CreatedAt,IsDeleted,TenantId)
VALUES ('{{Guid.NewGuid()}}','{{initializationId}}','{{evidence.AccountId}}',N'ghs',0,0,0,0,SYSUTCDATETIME(),0,'{{evidence.TenantId}}');
""");
        (await invalidCurrency.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(547);

        var lossyDown = () => database.ApplyAsync(up: false);
        (await lossyDown.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(51000);
        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM AccountingBookInitializations")).Should().Be(1);

        await database.ExecuteAsync("DELETE AccountingBookInitializationLines; DELETE AccountingBookInitializations; DELETE AccountingBookPeriods;");
        await database.ApplyAsync(up: false);
        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE name IN (N'AccountingBookPeriods',N'AccountingBookInitializations',N'AccountingBookInitializationLines')"))
            .Should().Be(0);
        (await database.ScalarAsync<int>($"SELECT COUNT(*) FROM AccountingBooks WHERE Id='{evidence.BookId}'"))
            .Should().Be(1, "bounded Down restores the exact predecessor tables without changing their evidence");
    }

    [SqlServerTheory]
    [InlineData("bad-code")]
    [InlineData("active-book")]
    [InlineData("pending-active")]
    [InlineData("cross-tenant-period")]
    [InlineData("schema-collision")]
    public async Task Migration_RejectsUnsafePredecessorBeforeAnyC4SchemaMutation(string corruption)
    {
        await using var database = await DisposableDatabase.CreateAsync();
        var evidence = await database.CreatePredecessorAsync();

        switch (corruption)
        {
            case "bad-code":
                await database.ExecuteAsync($"UPDATE AccountingBooks SET Code=N'ifrs' WHERE Id='{evidence.BookId}'");
                break;
            case "active-book":
                await database.ExecuteAsync($"UPDATE AccountingBooks SET LifecycleStatus=4,IsActive=1,AllowsPosting=1 WHERE Id='{evidence.BookId}'");
                break;
            case "pending-active":
                await database.ExecuteAsync($"UPDATE AccountingBooks SET PendingLifecycleStatus=4 WHERE Id='{evidence.BookId}'");
                break;
            case "cross-tenant-period":
                await database.ExecuteAsync($"UPDATE FiscalPeriods SET TenantId='{Guid.NewGuid()}' WHERE Id='{evidence.PeriodId}'");
                break;
            case "schema-collision":
                await database.ExecuteAsync("CREATE TABLE AccountingBookPeriods (Id uniqueidentifier NOT NULL PRIMARY KEY)");
                break;
        }

        var apply = () => database.ApplyAsync(up: true);
        (await apply.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(51000);
        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE name IN (N'AccountingBookInitializations',N'AccountingBookInitializationLines')"))
            .Should().Be(0, "the complete C4 preflight must fail before the first CreateTable operation");
    }

    [SqlServerFact]
    public async Task Migration_RejectsCrossTenantCutoff_AndCrossBookOrTenantSupersession()
    {
        await using var database = await DisposableDatabase.CreateAsync();
        var first = await database.CreatePredecessorAsync();
        var second = await database.AddTenantAuthorityAsync();
        await database.ApplyAsync(up: true);

        var firstInitializationId = Guid.NewGuid();
        await database.InsertInitializationAsync(
            first.TenantId, first.BookId, first.PeriodId, firstInitializationId,
            version: 1, key: "first", supersedesId: null);

        var crossTenantCutoff = () => database.InsertInitializationAsync(
            first.TenantId, first.BookId, second.PeriodId, Guid.NewGuid(),
            version: 2, key: "bad-cutoff", supersedesId: null);
        (await crossTenantCutoff.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(547);

        var parallelBookId = Guid.NewGuid();
        await database.ExecuteAsync($$"""
INSERT AccountingBooks VALUES ('{{parallelBookId}}','{{first.TenantId}}',N'LOCAL_STATUTORY',2,2,N'GHS',NULL,0,0,0,NULL,0);
""");
        var crossBookSupersession = () => database.InsertInitializationAsync(
            first.TenantId, parallelBookId, first.PeriodId, Guid.NewGuid(),
            version: 1, key: "bad-book", supersedesId: firstInitializationId);
        (await crossBookSupersession.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(547);

        var crossTenantSupersession = () => database.InsertInitializationAsync(
            second.TenantId, second.BookId, second.PeriodId, Guid.NewGuid(),
            version: 1, key: "bad-tenant", supersedesId: firstInitializationId);
        (await crossTenantSupersession.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(547);

        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM AccountingBookInitializations"))
            .Should().Be(1, "failed lineage writes must leave only the valid original authority");
    }

    private sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")))
                Skip = "Set RHEMA_TEST_SQLSERVER to run prefix-safe disposable C4 migration gates.";
        }
    }

    private sealed class SqlServerTheoryAttribute : TheoryAttribute
    {
        public SqlServerTheoryAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")))
                Skip = "Set RHEMA_TEST_SQLSERVER to run prefix-safe disposable C4 migration gates.";
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
            var name = $"RHEMAERP_GL_REHEARSAL_C4_{Guid.NewGuid():N}";
            if (!name.StartsWith("RHEMAERP_GL_REHEARSAL_", StringComparison.Ordinal))
                throw new InvalidOperationException("Disposable database prefix assertion failed.");
            var master = new SqlConnectionStringBuilder(configured) { InitialCatalog = "master", TrustServerCertificate = true };
            var target = new SqlConnectionStringBuilder(configured) { InitialCatalog = name, TrustServerCertificate = true };
            var database = new DisposableDatabase(name, master.ConnectionString, target.ConnectionString);
            await database.ExecuteMasterAsync($"CREATE DATABASE [{name}]");
            return database;
        }

        public async Task<(Guid TenantId, Guid BookId, Guid PeriodId, Guid AccountId)> CreatePredecessorAsync()
        {
            var tenantId = Guid.NewGuid();
            var bookId = Guid.NewGuid();
            var yearId = Guid.NewGuid();
            var periodId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            await ExecuteAsync($$"""
CREATE TABLE Tenants (Id uniqueidentifier NOT NULL CONSTRAINT PK_Tenants PRIMARY KEY, IsDeleted bit NOT NULL);
CREATE TABLE FiscalYears (Id uniqueidentifier NOT NULL CONSTRAINT PK_FiscalYears PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL);
CREATE TABLE FiscalPeriods (Id uniqueidentifier NOT NULL CONSTRAINT PK_FiscalPeriods PRIMARY KEY, TenantId uniqueidentifier NOT NULL, FiscalYearId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL, CONSTRAINT AK_FiscalPeriods_TenantId_Id UNIQUE(TenantId,Id));
CREATE TABLE Accounts (Id uniqueidentifier NOT NULL CONSTRAINT PK_Accounts PRIMARY KEY, TenantId uniqueidentifier NOT NULL, CONSTRAINT AK_Accounts_TenantId_Id UNIQUE(TenantId,Id));
CREATE TABLE AccountingBooks (
 Id uniqueidentifier NOT NULL CONSTRAINT PK_AccountingBooks PRIMARY KEY, TenantId uniqueidentifier NOT NULL, Code nvarchar(20) NOT NULL,
 BookType int NOT NULL, LifecycleStatus int NOT NULL, FunctionalCurrencyCode nvarchar(3) NULL, BaseAccountingBookId uniqueidentifier NULL,
 IsDefault bit NOT NULL, IsActive bit NOT NULL, AllowsPosting bit NOT NULL, PendingLifecycleStatus int NULL, IsDeleted bit NOT NULL,
 CONSTRAINT AK_AccountingBooks_TenantId_Id UNIQUE(TenantId,Id));
INSERT Tenants VALUES ('{{tenantId}}',0);
INSERT FiscalYears VALUES ('{{yearId}}','{{tenantId}}',0);
INSERT FiscalPeriods VALUES ('{{periodId}}','{{tenantId}}','{{yearId}}',0);
INSERT Accounts VALUES ('{{accountId}}','{{tenantId}}');
INSERT AccountingBooks VALUES ('{{bookId}}','{{tenantId}}',N'IFRS',1,2,N'GHS',NULL,1,0,0,NULL,0);
""");
            return (tenantId, bookId, periodId, accountId);
        }

        public async Task<(Guid TenantId, Guid BookId, Guid PeriodId, Guid AccountId)> AddTenantAuthorityAsync()
        {
            var tenantId = Guid.NewGuid();
            var bookId = Guid.NewGuid();
            var yearId = Guid.NewGuid();
            var periodId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            await ExecuteAsync($$"""
INSERT Tenants VALUES ('{{tenantId}}',0);
INSERT FiscalYears VALUES ('{{yearId}}','{{tenantId}}',0);
INSERT FiscalPeriods VALUES ('{{periodId}}','{{tenantId}}','{{yearId}}',0);
INSERT Accounts VALUES ('{{accountId}}','{{tenantId}}');
INSERT AccountingBooks VALUES ('{{bookId}}','{{tenantId}}',N'IFRS',1,2,N'GHS',NULL,1,0,0,NULL,0);
""");
            return (tenantId, bookId, periodId, accountId);
        }

        public Task InsertInitializationAsync(
            Guid tenantId,
            Guid bookId,
            Guid cutoffPeriodId,
            Guid initializationId,
            int version,
            string key,
            Guid? supersedesId)
        {
            var supersedes = supersedesId.HasValue ? $"'{supersedesId.Value}'" : "NULL";
            return ExecuteAsync($$"""
INSERT AccountingBookInitializations
 (Id,AccountingBookId,Version,SupersedesInitializationId,Mode,InitializationStatus,CutoffDate,CutoffFiscalPeriodId,
  IdempotencyKey,Reason,TotalDebits,TotalCredits,RequiredAccountCount,CoveredAccountCount,EvidenceFingerprint,
  ReconciliationFingerprint,PreparedByUserId,PreparedAtUtc,CreatedAt,IsDeleted,TenantId)
VALUES
 ('{{initializationId}}','{{bookId}}',{{version}},{{supersedes}},1,1,SYSUTCDATETIME(),'{{cutoffPeriodId}}',
  N'{{key}}',N'rehearsal',0,0,0,0,REPLICATE(N'A',64),REPLICATE(N'B',64),'{{Guid.NewGuid()}}',
  SYSUTCDATETIME(),SYSUTCDATETIME(),0,'{{tenantId}}');
""");
        }

        public async Task ApplyAsync(bool up)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(_connection).Options;
            await using var context = new ApplicationDbContext(options);
            var migration = new ExposedMigration();
            var operations = up ? migration.BuildUpOperations() : migration.BuildDownOperations();
            foreach (var command in context.GetService<IMigrationsSqlGenerator>().Generate(operations, context.Model))
                await ExecuteAsync(command.CommandText);
        }

        public async Task<T> ScalarAsync<T>(string sql)
        {
            await using var connection = new SqlConnection(_connection);
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection) { CommandTimeout = 600 };
            return (T)(await command.ExecuteScalarAsync())!;
        }

        public async Task ExecuteAsync(string sql)
        {
            await using var connection = new SqlConnection(_connection);
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection) { CommandTimeout = 600 };
            await command.ExecuteNonQueryAsync();
        }

        private async Task ExecuteMasterAsync(string sql)
        {
            await using var connection = new SqlConnection(_master);
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection) { CommandTimeout = 600 };
            await command.ExecuteNonQueryAsync();
        }

        public async ValueTask DisposeAsync()
        {
            if (!_name.StartsWith("RHEMAERP_GL_REHEARSAL_", StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing unsafe disposable database cleanup.");
            await ExecuteMasterAsync($"IF DB_ID(N'{_name}') IS NOT NULL BEGIN ALTER DATABASE [{_name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_name}]; END");
        }
    }

    private sealed class ExposedMigration : AddAccountingBookPeriodInitializationFoundation
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
