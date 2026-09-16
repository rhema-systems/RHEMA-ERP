using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class StablePostingBookIdentitySqlServerTests
{
    [SqlServerFact]
    [Trait("Category", "MultiBookIdentityC1")]
    public async Task Migration_ShouldBackfillExactBookEvidence_AndRestorePredecessorOnDown()
    {
        await using var database = await DisposableDatabase.CreateAsync();
        var evidence = await database.CreatePredecessorAsync("valid");
        await database.ApplyAsync(up: true);

        (await database.ScalarAsync<Guid>("SELECT AccountingBookId FROM JournalEntries;")).Should().Be(evidence.BookId);
        (await database.ScalarAsync<Guid>("SELECT AccountingBookId FROM AccountTransactions;")).Should().Be(evidence.BookId);
        (await database.ScalarAsync<Guid>("SELECT AccountingBookId FROM FinancePostingEvents;")).Should().Be(evidence.BookId);
        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM FinancePostingEvents WHERE RequestFingerprint IS NULL AND RequestFingerprintVersion IS NULL;"))
            .Should().Be(1, "the migration must not invent historical producer evidence");
        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM sys.foreign_keys WHERE name=N'FK_AccountTransactions_JournalEntries_TenantId_JournalEntryId_AccountingBookId';"))
            .Should().Be(1);

        await database.ApplyAsync(up: false);

        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'FinancePostingEvents') AND name IN (N'AccountingBookId',N'RequestFingerprint',N'RequestFingerprintVersion');"))
            .Should().Be(0);
        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID(N'FinancePostingEvents') AND name=N'IX_FinancePostingEvents_TenantId_IdempotencyKey';"))
            .Should().Be(1);
        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM sys.foreign_keys WHERE name=N'FK_AccountTransactions_JournalEntries_JournalEntryId';"))
            .Should().Be(1);
    }

    [SqlServerFact]
    [Trait("Category", "MultiBookIdentityC1")]
    public async Task Migration_ShouldRejectEveryNonCanonicalOrConflictingSnapshotBeforeMutation()
    {
        var cases = new[]
        {
            "blank", "pseudo", "unknown", "ambiguous", "cross-tenant",
            "book-case", "book-trailing", "journal-case", "journal-trailing", "transaction-case", "transaction-trailing",
            "event-case", "event-trailing", "transaction-mismatch", "event-mismatch"
        };

        foreach (var corruption in cases)
        {
            using var scope = new AssertionScope(corruption);
            await using var database = await DisposableDatabase.CreateAsync();
            await database.CreatePredecessorAsync(corruption);

            var action = () => database.ApplyAsync(up: true);

            (await action.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(51000);
            (await database.ScalarAsync<int>("SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'JournalEntries') AND name=N'AccountingBookId';"))
                .Should().Be(0, "the first migration operation is the complete preflight");
            (await database.ScalarAsync<int>("SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID(N'FinancePostingEvents') AND name=N'IX_FinancePostingEvents_TenantId_IdempotencyKey';"))
                .Should().Be(1);
        }
    }

    private sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")))
                Skip = "Set RHEMA_TEST_SQLSERVER to run the prefix-safe disposable C1 migration gates.";
        }
    }

    private sealed class DisposableDatabase : IAsyncDisposable
    {
        private readonly string _name;
        private readonly string _master;
        private readonly string _connection;

        private DisposableDatabase(string name, string master, string connection)
        {
            _name = name;
            _master = master;
            _connection = connection;
        }

        public static async Task<DisposableDatabase> CreateAsync()
        {
            var configured = Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")
                ?? throw new InvalidOperationException("RHEMA_TEST_SQLSERVER is required.");
            var name = $"RHEMAERP_GL_REHEARSAL_C1_{Guid.NewGuid():N}";
            if (!name.StartsWith("RHEMAERP_GL_REHEARSAL_", StringComparison.Ordinal))
                throw new InvalidOperationException("Disposable database prefix assertion failed.");
            var master = new SqlConnectionStringBuilder(configured) { InitialCatalog = "master", TrustServerCertificate = true };
            var target = new SqlConnectionStringBuilder(configured) { InitialCatalog = name, TrustServerCertificate = true };
            var database = new DisposableDatabase(name, master.ConnectionString, target.ConnectionString);
            await database.ExecuteMasterAsync($"CREATE DATABASE [{name}]");
            return database;
        }

        public async Task<(Guid TenantId, Guid BookId)> CreatePredecessorAsync(string corruption)
        {
            var tenantId = Guid.NewGuid();
            var bookTenantId = corruption == "cross-tenant" ? Guid.NewGuid() : tenantId;
            var bookId = Guid.NewGuid();
            var journalId = Guid.NewGuid();
            var bookCode = corruption switch
            {
                "book-case" => "ifrs",
                "book-trailing" => "IFRS ",
                _ => "IFRS"
            };
            var journalCode = corruption switch
            {
                "blank" => "",
                "pseudo" => "ALL_ACTIVE_BOOKS",
                "unknown" => "UNKNOWN",
                "journal-case" => "ifrs",
                "journal-trailing" => "IFRS ",
                "book-case" or "book-trailing" => bookCode,
                _ => "IFRS"
            };
            var transactionCode = corruption switch
            {
                "transaction-mismatch" => "LOCAL",
                "transaction-case" => "ifrs",
                "transaction-trailing" => "IFRS ",
                _ => journalCode
            };
            var eventCode = corruption switch
            {
                "event-mismatch" => "LOCAL",
                "event-case" => "ifrs",
                "event-trailing" => "IFRS ",
                _ => journalCode
            };
            var extraBooks = corruption == "ambiguous"
                ? $"INSERT AccountingBooks VALUES ('{Guid.NewGuid()}', '{tenantId}', N'IFRS', 0);"
                : corruption is "transaction-mismatch" or "event-mismatch"
                    ? $"INSERT AccountingBooks VALUES ('{Guid.NewGuid()}', '{tenantId}', N'LOCAL', 0);"
                    : string.Empty;

            await ExecuteAsync($$"""
CREATE TABLE AccountingBooks (Id uniqueidentifier NOT NULL CONSTRAINT PK_AccountingBooks PRIMARY KEY, TenantId uniqueidentifier NOT NULL, Code nvarchar(20) NOT NULL, IsDeleted bit NOT NULL);
CREATE TABLE JournalEntries (Id uniqueidentifier NOT NULL CONSTRAINT PK_JournalEntries PRIMARY KEY, TenantId uniqueidentifier NOT NULL, BookClassification nvarchar(20) NOT NULL);
CREATE TABLE AccountTransactions (Id uniqueidentifier NOT NULL CONSTRAINT PK_AccountTransactions PRIMARY KEY, TenantId uniqueidentifier NOT NULL, JournalEntryId uniqueidentifier NOT NULL, BookClassification nvarchar(20) NOT NULL, TransactionDate datetime2 NOT NULL, AccountId uniqueidentifier NOT NULL);
CREATE TABLE FinancePostingEvents (Id uniqueidentifier NOT NULL CONSTRAINT PK_FinancePostingEvents PRIMARY KEY, TenantId uniqueidentifier NOT NULL, JournalEntryId uniqueidentifier NULL, BookClassification nvarchar(20) NOT NULL, SourceModule nvarchar(50) NOT NULL, SourceDocumentType nvarchar(100) NOT NULL, SourceDocumentId uniqueidentifier NOT NULL, PostingAction nvarchar(50) NOT NULL, IdempotencyKey nvarchar(450) NULL, IsDeleted bit NOT NULL);
CREATE INDEX IX_AccountTransactions_JournalEntryId ON AccountTransactions(JournalEntryId);
CREATE INDEX IX_AccountTransactions_TenantId_BookClassification_TransactionDate_AccountId ON AccountTransactions(TenantId,BookClassification,TransactionDate,AccountId);
CREATE UNIQUE INDEX IX_FinancePostingEvents_TenantId_IdempotencyKey ON FinancePostingEvents(TenantId,IdempotencyKey) WHERE IsDeleted=0 AND IdempotencyKey IS NOT NULL;
CREATE UNIQUE INDEX IX_FinancePostingEvents_TenantId_SourceDocumentType_SourceDocumentId_PostingAction ON FinancePostingEvents(TenantId,SourceDocumentType,SourceDocumentId,PostingAction) WHERE IsDeleted=0;
CREATE UNIQUE INDEX IX_FinancePostingEvents_TenantId_SourceModule_SourceDocumentType_SourceDocumentId_PostingAction ON FinancePostingEvents(TenantId,SourceModule,SourceDocumentType,SourceDocumentId,PostingAction) WHERE IsDeleted=0;
CREATE INDEX IX_FinancePostingEvents_JournalEntryId ON FinancePostingEvents(JournalEntryId);
ALTER TABLE AccountTransactions ADD CONSTRAINT FK_AccountTransactions_JournalEntries_JournalEntryId FOREIGN KEY(JournalEntryId) REFERENCES JournalEntries(Id);
ALTER TABLE FinancePostingEvents ADD CONSTRAINT FK_FinancePostingEvents_JournalEntries_JournalEntryId FOREIGN KEY(JournalEntryId) REFERENCES JournalEntries(Id);
INSERT AccountingBooks VALUES ('{{bookId}}', '{{bookTenantId}}', N'{{bookCode.Replace("'", "''")}}', 0);
{{extraBooks}}
INSERT JournalEntries VALUES ('{{journalId}}', '{{tenantId}}', N'{{journalCode.Replace("'", "''")}}');
INSERT AccountTransactions VALUES ('{{Guid.NewGuid()}}', '{{tenantId}}', '{{journalId}}', N'{{transactionCode.Replace("'", "''")}}', SYSUTCDATETIME(), '{{Guid.NewGuid()}}');
INSERT FinancePostingEvents VALUES ('{{Guid.NewGuid()}}', '{{tenantId}}', '{{journalId}}', N'{{eventCode.Replace("'", "''")}}', N'TEST', N'C1Migration', '{{Guid.NewGuid()}}', N'Post', N'C1-MIGRATION', 0);
""");
            return (tenantId, bookId);
        }

        public async Task ApplyAsync(bool up)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(_connection).Options;
            await using var context = new ApplicationDbContext(options);
            var migration = new ExposedMigration();
            var operations = up ? migration.BuildUpOperations() : migration.BuildDownOperations();
            var generator = context.GetService<IMigrationsSqlGenerator>();
            foreach (var command in generator.Generate(operations, context.Model))
                await ExecuteAsync(command.CommandText);
        }

        public async Task ExecuteAsync(string sql)
        {
            await using var connection = new SqlConnection(_connection);
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection) { CommandTimeout = 600 };
            await command.ExecuteNonQueryAsync();
        }

        public async Task<T> ScalarAsync<T>(string sql)
        {
            await using var connection = new SqlConnection(_connection);
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection) { CommandTimeout = 600 };
            return (T)(await command.ExecuteScalarAsync())!;
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

        private sealed class ExposedMigration : AddStablePostingAccountingBookIdentity
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
