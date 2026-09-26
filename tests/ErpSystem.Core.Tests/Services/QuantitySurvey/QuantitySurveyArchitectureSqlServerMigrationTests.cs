using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

/// <summary>
/// Applies the current fresh-development migration chain to an isolated SQL Server database.
/// Set RHEMA_TEST_SQLSERVER to a SQL Server login that may create and drop databases.
/// </summary>
public sealed class QuantitySurveyArchitectureSqlServerMigrationTests
{
    private const string InitialBaselineMigrationId = "20260916132000_DisposableDevelopmentCurrentModelBaseline";

    [SqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    [Trait("Batch", "TDC-QS-ARCHITECTURE")]
    public async Task Supported_initial_baseline_materializes_before_forward_production_migrations()
    {
        await using var database = await DisposableSqlDatabase.CreateAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(database.ConnectionString, sql => sql.CommandTimeout(600))
            .Options;
        await using var context = new ApplicationDbContext(options, Guid.NewGuid());

        await ApplySupportedInitialBaselineAsync(context, database);

        var expectedBaselineHistory = context.Database.GetMigrations()
            .Where(id => string.CompareOrdinal(id, InitialBaselineMigrationId) <= 0)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        (await context.Database.GetAppliedMigrationsAsync()).Should().Equal(expectedBaselineHistory);
        (await context.Database.GetPendingMigrationsAsync()).Should().NotBeEmpty()
            .And.OnlyContain(id => string.CompareOrdinal(id, InitialBaselineMigrationId) > 0);

        var baselineTables = await database.QueryNamesAsync("""
            SELECT [name]
            FROM sys.tables
            WHERE [name] IN (N'AspNetRoles', N'Projects', N'Accounts', N'BusinessPartners')
            ORDER BY [name];
            """);
        baselineTables.Should().BeEquivalentTo([
            "Accounts", "AspNetRoles", "BusinessPartners", "Projects"
        ]);

        var untrustedForeignKeys = await database.QueryNamesAsync(
            "SELECT [name] FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1 ORDER BY [name];");
        untrustedForeignKeys.Should().BeEmpty();
    }

    [FullSqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    [Trait("Batch", "TDC-QS-ARCHITECTURE")]
    public async Task Current_development_migrations_create_a_trusted_complete_quantity_survey_schema()
    {
        await using var database = await DisposableSqlDatabase.CreateAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(database.ConnectionString, sql => sql.CommandTimeout(600))
            .Options;
        await using var context = new ApplicationDbContext(options, Guid.NewGuid());

        await ApplySupportedInitialBaselineAsync(context, database);
        await context.Database.MigrateAsync();

        (await context.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        (await context.Database.GetAppliedMigrationsAsync()).Should()
            .BeEquivalentTo(context.Database.GetMigrations());

        var untrustedForeignKeys = await database.QueryNamesAsync(
            "SELECT [name] FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1 ORDER BY [name];");
        untrustedForeignKeys.Should().BeEmpty("migration-created foreign keys must remain enabled and trusted");
        var untrustedChecks = await database.QueryNamesAsync(
            "SELECT [name] FROM sys.check_constraints WHERE is_disabled=1 OR is_not_trusted=1 ORDER BY [name];");
        untrustedChecks.Should().BeEmpty("migration-created check constraints must remain enabled and trusted");

        var requiredTables = new[]
        {
            "QuantitySurveyConfigurationProfiles", "ProjectBoqVersions",
            "QuantitySurveyEstimateVersions", "QuantitySurveyMeasurementSheets",
            "QuantitySurveyValuationWorksheets", "ProjectPaymentCertificates",
            "ProjectVariationOrders", "QuantitySurveyContractClaims",
            "QuantitySurveyDayworkSheets", "QuantitySurveySubcontracts",
            "QuantitySurveyMaterialReconciliations", "QuantitySurveyAdvanceRecoveryAgreements",
            "ProjectFinalAccounts"
        };
        var tables = await database.QueryNamesAsync(
            "SELECT [name] FROM sys.tables WHERE [name] LIKE N'QuantitySurvey%' OR [name] IN (N'ProjectBoqVersions',N'ProjectPaymentCertificates',N'ProjectVariationOrders',N'ProjectFinalAccounts');");
        tables.Should().Contain(requiredTables);

        var requiredTriggers = new[]
        {
            "TR_QsEstimateVersions_TenantAndLineageGuard",
            "TR_QsMeasurementSheets_Guard",
            "TR_QsValuationWorksheets_Guard",
            "TR_ProjectPaymentCertificates_QsLifecycle",
            "TR_QS0508_ProjectVariation_Governance",
            "TR_QS0509_ContractClaims_Governance",
            "TR_QS0510_DayworkSheets_Governance",
            "TR_QS0521_Subcontracts_Governance",
            "TR_QS0507_MaterialReconciliation_Governance",
            "TR_QsAdvanceRecoveryAgreements_QS0505Guard",
            "TR_ProjectFinalAccounts_QS0506Guard"
        };
        var triggers = await database.QueryNamesAsync(
            "SELECT [name] FROM sys.triggers WHERE parent_class=1 AND is_ms_shipped=0 AND is_disabled=0;");
        triggers.Should().Contain(requiredTriggers);

        var estimateColumns = await database.QueryNamesAsync(
            "SELECT [name] FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.QuantitySurveyEstimateVersions');");
        estimateColumns.Should().Contain([
            "FundingSourceSnapshot", "PropertyReferenceSnapshot", "SourceSnapshotSchemaVersion"
        ]);
    }

    private static async Task ApplySupportedInitialBaselineAsync(
        ApplicationDbContext context,
        DisposableSqlDatabase database)
    {
        // Finance PR #219 replaced the archived upgrade history with a fresh-database
        // baseline. Exercise that real chain without stamping synthetic history rows.
        // DisposableSqlDatabase always creates a new random database for this test.
        await context.GetService<IMigrator>().MigrateAsync(InitialBaselineMigrationId);
    }

    [SqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    [Trait("Batch", "TDC-QS-ARCHITECTURE")]
    public async Task Estimate_source_snapshot_migration_applies_to_real_sql_server_and_enforces_its_constraint()
    {
        await using var database = await DisposableSqlDatabase.CreateAsync();
        await database.ExecuteSqlAsync("""
            CREATE TABLE [dbo].[QuantitySurveyEstimateVersions]
            (
                [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_QuantitySurveyEstimateVersions] PRIMARY KEY
            );
            """);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(database.ConnectionString)
            .Options;
        await using var context = new ApplicationDbContext(options, Guid.NewGuid());
        var sqlGenerator = context.GetService<IMigrationsSqlGenerator>();
        var migration = new AddQuantitySurveyEstimateSourceSnapshots();
        var commands = sqlGenerator.Generate(migration.UpOperations, context.Model);

        foreach (var command in commands)
            await database.ExecuteSqlAsync(command.CommandText);

        var columns = await database.QueryNamesAsync(
            "SELECT [name] FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.QuantitySurveyEstimateVersions');");
        columns.Should().Contain([
            "FundingSourceSnapshot", "PropertyReferenceSnapshot", "SourceSnapshotSchemaVersion"
        ]);

        var trustedChecks = await database.QueryNamesAsync("""
            SELECT [name]
            FROM sys.check_constraints
            WHERE parent_object_id=OBJECT_ID(N'dbo.QuantitySurveyEstimateVersions')
              AND [name]=N'CK_QsEstimateVersions_SourceSnapshotSchema'
              AND is_disabled=0
              AND is_not_trusted=0;
            """);
        trustedChecks.Should().ContainSingle()
            .Which.Should().Be("CK_QsEstimateVersions_SourceSnapshotSchema");

        await database.ExecuteSqlAsync(
            $"INSERT INTO [dbo].[QuantitySurveyEstimateVersions] ([Id]) VALUES ('{Guid.NewGuid()}');");
        var defaults = await database.QueryNamesAsync(
            "SELECT CONVERT(nvarchar(10), [SourceSnapshotSchemaVersion]) FROM [dbo].[QuantitySurveyEstimateVersions];");
        defaults.Should().Equal("0");

        Func<Task> invalidInsert = () => database.ExecuteSqlAsync(
            $"INSERT INTO [dbo].[QuantitySurveyEstimateVersions] ([Id], [SourceSnapshotSchemaVersion]) VALUES ('{Guid.NewGuid()}', 2);");
        (await invalidInsert.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(547);
    }

    [SqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    [Trait("Batch", "TDC-VPS-MIGRATION-COMPATIBILITY")]
    public async Task Pending_finance_migration_repairs_the_legacy_supplier_debit_note_schema()
    {
        await using var database = await DisposableSqlDatabase.CreateAsync();
        await database.ExecuteSqlAsync("""
            CREATE TABLE [dbo].[BusinessPartners] ([Id] uniqueidentifier NOT NULL CONSTRAINT [PK_BusinessPartners] PRIMARY KEY);
            CREATE TABLE [dbo].[Tenants] ([Id] uniqueidentifier NOT NULL CONSTRAINT [PK_Tenants] PRIMARY KEY);
            CREATE TABLE [dbo].[VendorInvoice] ([Id] uniqueidentifier NOT NULL CONSTRAINT [PK_VendorInvoice] PRIMARY KEY);
            CREATE TABLE [dbo].[JournalEntries] ([Id] uniqueidentifier NOT NULL CONSTRAINT [PK_JournalEntries] PRIMARY KEY);
            CREATE TABLE [dbo].[FinancePostingEvents] ([Id] uniqueidentifier NOT NULL CONSTRAINT [PK_FinancePostingEvents] PRIMARY KEY);
            CREATE TABLE [dbo].[WorkflowInstances] ([Id] uniqueidentifier NOT NULL CONSTRAINT [PK_WorkflowInstances] PRIMARY KEY);
            CREATE TABLE [dbo].[Accounts] ([Id] uniqueidentifier NOT NULL CONSTRAINT [PK_Accounts] PRIMARY KEY);
            CREATE TABLE [dbo].[VendorPayment] ([Id] uniqueidentifier NOT NULL CONSTRAINT [PK_VendorPayment] PRIMARY KEY);

            CREATE TABLE [dbo].[SupplierDebitNotes]
            (
                [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_SupplierDebitNotes] PRIMARY KEY,
                [DebitNoteNumber] nvarchar(50) NOT NULL,
                [VendorId] uniqueidentifier NOT NULL,
                [SupplierReturnId] uniqueidentifier NULL,
                [OriginalVendorInvoiceId] uniqueidentifier NULL,
                [DebitNoteDate] datetime2 NOT NULL,
                [CurrencyCode] nvarchar(3) NOT NULL,
                [ExchangeRate] decimal(18,4) NOT NULL,
                [SubTotal] decimal(18,2) NOT NULL,
                [TaxAmount] decimal(18,2) NOT NULL,
                [TotalAmount] decimal(18,2) NOT NULL,
                [BaseCurrencyAmount] decimal(18,2) NOT NULL,
                [Status] int NOT NULL,
                [JournalEntryId] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL,
                [TenantId] uniqueidentifier NOT NULL
            );

            CREATE TABLE [dbo].[SupplierDebitNoteLineItems]
            (
                [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_SupplierDebitNoteLineItems] PRIMARY KEY,
                [SupplierDebitNoteId] uniqueidentifier NOT NULL,
                [Description] nvarchar(500) NOT NULL,
                [Quantity] decimal(18,4) NOT NULL,
                [UnitPrice] decimal(18,2) NOT NULL,
                [TaxGroupId] uniqueidentifier NULL,
                [TaxRate] decimal(18,4) NOT NULL,
                [TaxAmount] decimal(18,2) NOT NULL,
                [LineTotal] decimal(18,2) NOT NULL,
                [IsDeleted] bit NOT NULL,
                [TenantId] uniqueidentifier NOT NULL
            );
            """);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(database.ConnectionString)
            .Options;
        await using var context = new ApplicationDbContext(options, Guid.NewGuid());
        var sqlGenerator = context.GetService<IMigrationsSqlGenerator>();
        var migration = new AddSupplierDebitNoteLifecycleAndApplications();
        var commands = sqlGenerator.Generate(migration.UpOperations, context.Model);

        foreach (var command in commands)
            await database.ExecuteSqlAsync(command.CommandText);

        var repairedColumns = await database.QueryNamesAsync(
            "SELECT [name] FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.SupplierDebitNotes');");
        repairedColumns.Should().Contain([
            "SupplierCreditNoteReference", "PostingEventId", "WorkflowInstanceId", "RowVersion"
        ]);

        var indexes = await database.QueryNamesAsync("""
            SELECT [name]
            FROM sys.indexes
            WHERE object_id IN
            (
                OBJECT_ID(N'dbo.SupplierDebitNotes'),
                OBJECT_ID(N'dbo.SupplierDebitNoteLineItems'),
                OBJECT_ID(N'dbo.SupplierDebitNoteApplications')
            )
              AND [name] IS NOT NULL;
            """);
        indexes.Should().Contain([
            "UX_SupplierDebitNotes_Tenant_Vendor_SupplierReference",
            "IX_SupplierDebitNoteLineItems_GLAccountId",
            "UX_SupplierDebitNoteApplication_Tenant_Original_Reversal"
        ]);

        var exchangeRateScale = await database.QueryNamesAsync("""
            SELECT CONVERT(nvarchar(10), [scale])
            FROM sys.columns
            WHERE object_id=OBJECT_ID(N'dbo.SupplierDebitNotes') AND [name]=N'ExchangeRate';
            """);
        exchangeRateScale.Should().Equal("6");
    }

    [SqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task Audit_idempotency_repair_preserves_history_and_enforces_per_tenant_retries()
    {
        await using var database = await DisposableSqlDatabase.CreateAsync();
        await database.ExecuteSqlAsync("""
            CREATE TABLE dbo.AuditLogs (Id int NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL);
            INSERT dbo.AuditLogs VALUES (1,'00000000-0000-0000-0000-000000000001');
            """);
        await database.ExecuteSqlAsync(ReconcileAuditLogIdempotencyForExistingDatabases.ReconciliationSql);
        await database.ExecuteSqlAsync(ReconcileAuditLogIdempotencyForExistingDatabases.ReconciliationSql);
        await database.ExecuteSqlAsync("""
            INSERT dbo.AuditLogs VALUES
                (2,'00000000-0000-0000-0000-000000000001',NULL),
                (3,'00000000-0000-0000-0000-000000000001',N'retry'),
                (4,'00000000-0000-0000-0000-000000000002',N'retry');
            """);
        (await database.QueryNamesAsync("SELECT CONVERT(nvarchar(10),Id) FROM dbo.AuditLogs ORDER BY Id;"))
            .Should().Equal("1", "2", "3", "4");
        Func<Task> duplicate = () => database.ExecuteSqlAsync("""
            INSERT dbo.AuditLogs VALUES (5,'00000000-0000-0000-0000-000000000001',N'retry');
            """);
        (await duplicate.Should().ThrowAsync<SqlException>()).Which.Number.Should().BeOneOf(2601, 2627);
    }

    private sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")))
                Skip = "Set RHEMA_TEST_SQLSERVER to run the disposable Quantity Survey migration gate.";
        }
    }

    private sealed class FullSqlServerFactAttribute : FactAttribute
    {
        public FullSqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER_FULL_MIGRATIONS")))
                Skip = "Set RHEMA_TEST_SQLSERVER_FULL_MIGRATIONS to run the full production migration-chain gate.";
        }
    }

    private sealed class DisposableSqlDatabase : IAsyncDisposable
    {
        private readonly string _databaseName;
        private readonly string _masterConnectionString;

        private DisposableSqlDatabase(string databaseName, string connectionString, string masterConnectionString)
        {
            _databaseName = databaseName;
            ConnectionString = connectionString;
            _masterConnectionString = masterConnectionString;
        }

        public string ConnectionString { get; }

        public static async Task<DisposableSqlDatabase> CreateAsync()
        {
            var baseConnection = Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")
                ?? throw new InvalidOperationException("RHEMA_TEST_SQLSERVER is required.");
            // This QS gate exercises the shared production chain and creates/drops a real SQL
            // Server database. The Stage A prefix is therefore a cross-owner safety contract,
            // not a QS schema assertion; compatibility-specific coverage lives in Finance tests.
            var databaseName = $"RHEMAERP_GL_REHEARSAL_MIGRATION_{Guid.NewGuid():N}";
            var masterBuilder = new SqlConnectionStringBuilder(baseConnection)
            {
                InitialCatalog = "master",
                TrustServerCertificate = true
            };
            var databaseBuilder = new SqlConnectionStringBuilder(baseConnection)
            {
                InitialCatalog = databaseName,
                TrustServerCertificate = true
            };
            var result = new DisposableSqlDatabase(databaseName, databaseBuilder.ConnectionString,
                masterBuilder.ConnectionString);
            await using var master = new SqlConnection(result._masterConnectionString);
            await master.OpenAsync();
            await ExecuteAsync(master, $"CREATE DATABASE [{databaseName}];");
            return result;
        }

        public async Task<IReadOnlyList<string>> QueryNamesAsync(string sql)
        {
            var result = new List<string>();
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = 600;
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) result.Add(reader.GetString(0));
            return result;
        }

        public async Task ExecuteSqlAsync(string sql)
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await ExecuteAsync(connection, sql);
        }

        private static async Task ExecuteAsync(SqlConnection connection, string sql)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = 600;
            await command.ExecuteNonQueryAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await using var master = new SqlConnection(_masterConnectionString);
            await master.OpenAsync();
            await ExecuteAsync(master,
                $"IF DB_ID(N'{_databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_databaseName}]; END;");
        }
    }
}
