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

public sealed class BookBalanceMigrationC2SqlServerTests
{
    [SqlServerFact]
    public async Task Migration_BackfillsExactBook_AndRestoresPredecessor()
    {
        await using var database = await DisposableDatabase.CreateAsync();
        var evidence = await database.CreatePredecessorAsync("IFRS", "GHS");
        await database.ApplyAsync(up: true);

        (await database.ScalarAsync<Guid>("SELECT AccountingBookId FROM AccountBalances"))
            .Should().Be(evidence.BookId);
        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM sys.foreign_keys WHERE name=N'FK_AccountBalances_Accounts_TenantId_AccountId'"))
            .Should().Be(1);
        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE name IN (N'AccountCurrencyExposures',N'FinanceBalanceRebuildRuns')"))
            .Should().Be(2);

        await database.ApplyAsync(up: false);
        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'AccountBalances') AND name=N'AccountingBookId'"))
            .Should().Be(0);
        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM sys.foreign_keys WHERE name=N'FK_AccountBalances_Accounts_AccountId'"))
            .Should().Be(1);
    }

    [SqlServerFact]
    public async Task Migration_RejectsNonCanonicalEvidenceBeforeMutation()
    {
        var corruptions = new[]
        {
            ("ifrs", "GHS", false, false, false, false),
            ("IFRS ", "GHS", false, false, false, false),
            ("ALL_ACTIVE_BOOKS", "GHS", false, false, false, false),
            ("IFRS", "", false, false, false, false),
            ("IFRS", "GHS", true, false, false, false),
            ("IFRS", "GHS", false, true, false, false),
            ("IFRS", "GHS", false, false, true, false),
            ("IFRS", "GHS", false, false, false, true)
        };
        foreach (var (bookCode, currency, crossBook, crossAccount, crossPeriod, ambiguousBook) in corruptions)
        {
            await using var database = await DisposableDatabase.CreateAsync();
            await database.CreatePredecessorAsync(bookCode, currency, crossBook, crossAccount, crossPeriod, ambiguousBook);

            var action = () => database.ApplyAsync(up: true);

            (await action.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(51000);
            (await database.ScalarAsync<int>("SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'AccountBalances') AND name=N'AccountingBookId'"))
                .Should().Be(0, "the complete preflight is the first migration operation");
        }
    }

    private sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")))
                Skip = "Set RHEMA_TEST_SQLSERVER to run prefix-safe disposable C2 migration gates.";
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
            var name = $"RHEMAERP_GL_REHEARSAL_C2_{Guid.NewGuid():N}";
            if (!name.StartsWith("RHEMAERP_GL_REHEARSAL_", StringComparison.Ordinal))
                throw new InvalidOperationException("Disposable database prefix assertion failed.");
            var master = new SqlConnectionStringBuilder(configured) { InitialCatalog = "master", TrustServerCertificate = true };
            var target = new SqlConnectionStringBuilder(configured) { InitialCatalog = name, TrustServerCertificate = true };
            var result = new DisposableDatabase(name, master.ConnectionString, target.ConnectionString);
            await result.ExecuteMasterAsync($"CREATE DATABASE [{name}]");
            return result;
        }

        public async Task<(Guid TenantId, Guid BookId)> CreatePredecessorAsync(string balanceBookCode, string currency,
            bool crossTenantBook = false, bool crossTenantAccount = false, bool crossTenantPeriod = false,
            bool ambiguousBook = false)
        {
            var tenantId = Guid.NewGuid(); var bookId = Guid.NewGuid(); var accountId = Guid.NewGuid(); var periodId = Guid.NewGuid();
            var bookTenantId = crossTenantBook ? Guid.NewGuid() : tenantId;
            var accountTenantId = crossTenantAccount ? Guid.NewGuid() : tenantId;
            var periodTenantId = crossTenantPeriod ? Guid.NewGuid() : tenantId;
            var duplicateBook = ambiguousBook
                ? $"INSERT AccountingBooks VALUES ('{Guid.NewGuid()}','{tenantId}',N'IFRS',0);" : string.Empty;
            await ExecuteAsync($$"""
CREATE TABLE Tenants (Id uniqueidentifier NOT NULL CONSTRAINT PK_Tenants PRIMARY KEY);
CREATE TABLE AccountingBooks (Id uniqueidentifier NOT NULL CONSTRAINT PK_AccountingBooks PRIMARY KEY, TenantId uniqueidentifier NOT NULL, Code nvarchar(20) NOT NULL, IsDeleted bit NOT NULL, CONSTRAINT AK_AccountingBooks_TenantId_Id UNIQUE(TenantId,Id));
CREATE TABLE Accounts (Id uniqueidentifier NOT NULL CONSTRAINT PK_Accounts PRIMARY KEY, TenantId uniqueidentifier NOT NULL, CONSTRAINT AK_Accounts_TenantId_Id UNIQUE(TenantId,Id));
CREATE TABLE FiscalPeriods (Id uniqueidentifier NOT NULL CONSTRAINT PK_FiscalPeriods PRIMARY KEY, TenantId uniqueidentifier NOT NULL);
CREATE INDEX IX_FiscalPeriods_TenantId ON FiscalPeriods(TenantId);
CREATE TABLE AccountBalances (Id uniqueidentifier NOT NULL CONSTRAINT PK_AccountBalances PRIMARY KEY, TenantId uniqueidentifier NOT NULL, AccountId uniqueidentifier NOT NULL, FiscalPeriodId uniqueidentifier NOT NULL, BookClassification nvarchar(20) NOT NULL, Currency nvarchar(3) NULL);
CREATE INDEX IX_AccountBalances_AccountId ON AccountBalances(AccountId);
CREATE INDEX IX_AccountBalances_FiscalPeriodId ON AccountBalances(FiscalPeriodId);
CREATE UNIQUE INDEX IX_AccountBalances_TenantId_AccountId_FiscalPeriodId_BookClassification_Currency ON AccountBalances(TenantId,AccountId,FiscalPeriodId,BookClassification,Currency);
ALTER TABLE AccountBalances ADD CONSTRAINT FK_AccountBalances_Accounts_AccountId FOREIGN KEY(AccountId) REFERENCES Accounts(Id);
ALTER TABLE AccountBalances ADD CONSTRAINT FK_AccountBalances_FiscalPeriods_FiscalPeriodId FOREIGN KEY(FiscalPeriodId) REFERENCES FiscalPeriods(Id);
INSERT Tenants VALUES ('{{tenantId}}');
INSERT AccountingBooks VALUES ('{{bookId}}','{{bookTenantId}}',N'IFRS',0);
{{duplicateBook}}
INSERT Accounts VALUES ('{{accountId}}','{{accountTenantId}}');
INSERT FiscalPeriods VALUES ('{{periodId}}','{{periodTenantId}}');
INSERT AccountBalances VALUES ('{{Guid.NewGuid()}}','{{tenantId}}','{{accountId}}','{{periodId}}',N'{{balanceBookCode.Replace("'", "''")}}',N'{{currency.Replace("'", "''")}}');
""");
            return (tenantId, bookId);
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

        private async Task ExecuteAsync(string sql)
        {
            await using var connection = new SqlConnection(_connection); await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection) { CommandTimeout = 600 };
            await command.ExecuteNonQueryAsync();
        }

        public async Task<T> ScalarAsync<T>(string sql)
        {
            await using var connection = new SqlConnection(_connection); await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection) { CommandTimeout = 600 };
            return (T)(await command.ExecuteScalarAsync())!;
        }

        private async Task ExecuteMasterAsync(string sql)
        {
            await using var connection = new SqlConnection(_master); await connection.OpenAsync();
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

    private sealed class ExposedMigration : AddBookAwareBalanceFoundation
    {
        public IReadOnlyList<MigrationOperation> BuildUpOperations() { var b = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer"); Up(b); return b.Operations; }
        public IReadOnlyList<MigrationOperation> BuildDownOperations() { var b = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer"); Down(b); return b.Operations; }
    }
}
