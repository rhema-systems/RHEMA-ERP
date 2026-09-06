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

public sealed class AccountingBookLifecycleC3MigrationSqlServerTests
{
    [SqlServerFact]
    public async Task Migration_ReconcilesLegacyBooks_AndRestoresPredecessorShape()
    {
        await using var database = await DisposableDatabase.CreateAsync();
        var evidence = await database.CreatePredecessorAsync("IFRS", "GHS", defaults: 1);
        await database.ApplyAsync(up: true);

        (await database.ScalarAsync<int>($"SELECT BookType FROM AccountingBooks WHERE Id='{evidence.BookId}'"))
            .Should().Be(1);
        (await database.ScalarAsync<int>($"SELECT LifecycleStatus FROM AccountingBooks WHERE Id='{evidence.BookId}'"))
            .Should().Be(4);
        (await database.ScalarAsync<string>($"SELECT FunctionalCurrencyCode FROM AccountingBooks WHERE Id='{evidence.BookId}'"))
            .Should().Be("GHS");
        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM sys.foreign_keys WHERE name=N'FK_AccountingBooks_AccountingBooks_TenantId_BaseAccountingBookId'"))
            .Should().Be(1);

        await database.ApplyAsync(up: false);
        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'AccountingBooks') AND name=N'BookType'"))
            .Should().Be(0);
        (await database.ScalarAsync<int>($"SELECT COUNT(*) FROM AccountingBooks WHERE Id='{evidence.BookId}' AND Code=N'IFRS'"))
            .Should().Be(1, "downgrade retains the predecessor stable row and identity");
    }

    [SqlServerFact]
    public async Task Migration_RejectsUnsafeLegacyAuthorityBeforeAddingColumns()
    {
        var cases = new[]
        {
            (Code: "ifrs", Currency: "GHS", Defaults: 1, JournalCode: (string?)null, Active: true, Posting: true),
            (Code: "IFRS ", Currency: "GHS", Defaults: 1, JournalCode: (string?)null, Active: true, Posting: true),
            (Code: "ALL_ACTIVE_BOOKS", Currency: "GHS", Defaults: 1, JournalCode: (string?)null, Active: true, Posting: true),
            (Code: "ALL_CLASSIFIED_BOOKS", Currency: "GHS", Defaults: 1, JournalCode: (string?)null, Active: true, Posting: true),
            (Code: "ALLCLASSIFIEDBOOKS", Currency: "GHS", Defaults: 1, JournalCode: (string?)null, Active: true, Posting: true),
            (Code: "ALL", Currency: "GHS", Defaults: 1, JournalCode: (string?)null, Active: true, Posting: true),
            (Code: "IFRS", Currency: "ghs", Defaults: 1, JournalCode: (string?)null, Active: true, Posting: true),
            (Code: "IFRS", Currency: "GHS", Defaults: 0, JournalCode: (string?)null, Active: true, Posting: true),
            (Code: "IFRS", Currency: "GHS", Defaults: 2, JournalCode: (string?)null, Active: true, Posting: true),
            (Code: "IFRS", Currency: "GHS", Defaults: 1, JournalCode: "ifrs", Active: true, Posting: true),
            (Code: "IFRS", Currency: "GHS", Defaults: 1, JournalCode: (string?)null, Active: true, Posting: false)
        };

        foreach (var item in cases)
        {
            await using var database = await DisposableDatabase.CreateAsync();
            await database.CreatePredecessorAsync(item.Code, item.Currency, item.Defaults, item.JournalCode, item.Active, item.Posting);

            var action = () => database.ApplyAsync(up: true);

            (await action.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(51000);
            (await database.ScalarAsync<int>("SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'AccountingBooks') AND name=N'BookType'"))
                .Should().Be(0, "the complete preflight precedes every schema mutation");
        }
    }

    private sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")))
                Skip = "Set RHEMA_TEST_SQLSERVER to run prefix-safe disposable C3 migration gates.";
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
            var name = $"RHEMAERP_GL_REHEARSAL_C3_{Guid.NewGuid():N}";
            if (!name.StartsWith("RHEMAERP_GL_REHEARSAL_", StringComparison.Ordinal))
                throw new InvalidOperationException("Disposable database prefix assertion failed.");
            var master = new SqlConnectionStringBuilder(configured) { InitialCatalog = "master", TrustServerCertificate = true };
            var target = new SqlConnectionStringBuilder(configured) { InitialCatalog = name, TrustServerCertificate = true };
            var database = new DisposableDatabase(name, master.ConnectionString, target.ConnectionString);
            await database.ExecuteMasterAsync($"CREATE DATABASE [{name}]");
            return database;
        }

        public async Task<(Guid TenantId, Guid BookId)> CreatePredecessorAsync(
            string code, string currency, int defaults, string? journalCode = null,
            bool active = true, bool posting = true)
        {
            var tenantId = Guid.NewGuid();
            var bookId = Guid.NewGuid();
            var secondBookId = Guid.NewGuid();
            var journalId = Guid.NewGuid();
            var secondDefault = defaults == 2 ? 1 : 0;
            var firstDefault = defaults == 0 ? 0 : 1;
            var journal = journalCode == null ? string.Empty :
                $"INSERT JournalEntries VALUES ('{journalId}','{tenantId}','{bookId}',N'{Esc(journalCode)}',0);";
            await ExecuteAsync($$"""
CREATE TABLE Tenants (Id uniqueidentifier NOT NULL CONSTRAINT PK_Tenants PRIMARY KEY, BaseCurrency nvarchar(3) NOT NULL, IsDeleted bit NOT NULL);
CREATE TABLE FinanceSettings (Id uniqueidentifier NOT NULL CONSTRAINT PK_FinanceSettings PRIMARY KEY, TenantId uniqueidentifier NOT NULL, BaseCurrency nvarchar(3) NOT NULL, IsDeleted bit NOT NULL);
CREATE TABLE AccountingBooks (Id uniqueidentifier NOT NULL CONSTRAINT PK_AccountingBooks PRIMARY KEY, TenantId uniqueidentifier NOT NULL, Code nvarchar(20) NOT NULL, Name nvarchar(100) NOT NULL, Description nvarchar(500) NULL, Purpose nvarchar(50) NOT NULL, IsActive bit NOT NULL, IsDefault bit NOT NULL, AllowsPosting bit NOT NULL, IsSystemDefined bit NOT NULL, SortOrder int NOT NULL, CreatedAt datetime2 NOT NULL, CreatedBy nvarchar(max) NULL, CreatedById uniqueidentifier NULL, UpdatedAt datetime2 NULL, UpdatedBy nvarchar(max) NULL, LastModifiedById uniqueidentifier NULL, IsDeleted bit NOT NULL, DeletedAt datetime2 NULL, DeletedBy nvarchar(max) NULL, CONSTRAINT AK_AccountingBooks_TenantId_Id UNIQUE(TenantId,Id));
CREATE TABLE JournalEntries (Id uniqueidentifier NOT NULL CONSTRAINT PK_JournalEntries PRIMARY KEY, TenantId uniqueidentifier NOT NULL, AccountingBookId uniqueidentifier NOT NULL, BookClassification nvarchar(20) NOT NULL, IsDeleted bit NOT NULL);
CREATE TABLE AccountTransactions (Id uniqueidentifier NOT NULL CONSTRAINT PK_AccountTransactions PRIMARY KEY, TenantId uniqueidentifier NOT NULL, AccountingBookId uniqueidentifier NOT NULL, JournalEntryId uniqueidentifier NOT NULL, BookClassification nvarchar(20) NOT NULL, IsDeleted bit NOT NULL);
CREATE TABLE FinancePostingEvents (Id uniqueidentifier NOT NULL CONSTRAINT PK_FinancePostingEvents PRIMARY KEY, TenantId uniqueidentifier NOT NULL, AccountingBookId uniqueidentifier NOT NULL, JournalEntryId uniqueidentifier NOT NULL, BookClassification nvarchar(20) NOT NULL, IsDeleted bit NOT NULL);
INSERT Tenants VALUES ('{{tenantId}}',N'{{Esc(currency)}}',0);
INSERT FinanceSettings VALUES ('{{Guid.NewGuid()}}','{{tenantId}}',N'{{Esc(currency)}}',0);
INSERT AccountingBooks VALUES ('{{bookId}}','{{tenantId}}',N'{{Esc(code)}}',N'Primary',NULL,N'Primary',{{(active ? 1 : 0)}},{{firstDefault}},{{(posting ? 1 : 0)}},1,10,SYSUTCDATETIME(),NULL,NULL,NULL,NULL,NULL,0,NULL,NULL);
INSERT AccountingBooks VALUES ('{{secondBookId}}','{{tenantId}}',N'LOCAL_STATUTORY',N'Local',NULL,N'Statutory',1,{{secondDefault}},1,1,20,SYSUTCDATETIME(),NULL,NULL,NULL,NULL,NULL,0,NULL,NULL);
{{journal}}
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

        public async Task<T> ScalarAsync<T>(string sql)
        {
            await using var connection = new SqlConnection(_connection);
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection) { CommandTimeout = 600 };
            return (T)(await command.ExecuteScalarAsync())!;
        }

        private async Task ExecuteAsync(string sql)
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

        private static string Esc(string value) => value.Replace("'", "''", StringComparison.Ordinal);

        public async ValueTask DisposeAsync()
        {
            if (!_name.StartsWith("RHEMAERP_GL_REHEARSAL_", StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing unsafe disposable database cleanup.");
            await ExecuteMasterAsync($"IF DB_ID(N'{_name}') IS NOT NULL BEGIN ALTER DATABASE [{_name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_name}]; END");
        }
    }

    private sealed class ExposedMigration : AddGovernedAccountingBookLifecycle
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
