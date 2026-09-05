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

/// <summary>
/// SQL Server evidence for shared historical-chain compatibility owned by the Stage A
/// migration/reset rehearsal. Every database is disposable and prefix-guarded.
/// </summary>
public sealed class MigrationCompatibilitySqlServerTests
{
    private const string InitialBaseline = "20260313114533_InitialBaseline";
    private const string CrmSales = "20260315081834_AddCrmSalesEntities";
    private const string CrmCampaign = "20260315150304_AddCrmCampaignEntities";
    private const string CrmPromotion = "20260317115118_AddCrmEntities";
    private const string ProjectPredecessor = "20260317122200_AddCompetitorEntities";
    private const string ProjectFoundation = "20260407033921_AddProjectPackageBoqFoundation";

    [SqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task CrmCompatibility_DropsOnlyCompleteEmptyPredecessorGraph()
    {
        await using var database = await DisposableSqlDatabase.CreateAsync();
        await database.ExecuteAsync(CreateRedundantCrmGraphSql);

        await database.ExecuteAsync(GetCrmCompatibilitySql());

        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE [name] IN (N'Leads',N'Opportunities',N'Activities',N'Quotes',N'QuoteLineItems',N'Campaigns',N'CampaignMembers');"))
            .Should().Be(0);
    }

    [SqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task CrmCompatibility_PartialGraphFailsBeforeMutation()
    {
        await using var database = await DisposableSqlDatabase.CreateAsync();
        await database.ExecuteAsync("CREATE TABLE [dbo].[Leads] ([Id] int NOT NULL CONSTRAINT [PK_TestLeads] PRIMARY KEY); INSERT INTO [dbo].[Leads] VALUES (7);");

        var action = () => database.ExecuteAsync(GetCrmCompatibilitySql());

        (await action.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(51000);
        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM [dbo].[Leads];")).Should().Be(1);
    }

    [SqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task CrmCompatibility_PopulatedCompleteGraphFailsBeforeMutation()
    {
        await using var database = await DisposableSqlDatabase.CreateAsync();
        await database.ExecuteAsync(CreateRedundantCrmGraphSql);
        await database.ExecuteAsync("INSERT INTO [dbo].[QuoteLineItems] VALUES (11);");

        var action = () => database.ExecuteAsync(GetCrmCompatibilitySql());

        (await action.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(51000);
        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE [name] IN (N'Leads',N'Opportunities',N'Activities',N'Quotes',N'QuoteLineItems',N'Campaigns',N'CampaignMembers');"))
            .Should().Be(7);
        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM [dbo].[QuoteLineItems];")).Should().Be(1);
    }

    [SqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task VendorInvoiceCompatibility_PreservesRowsForwardAndBackward()
    {
        await using var database = await DisposableSqlDatabase.CreateAsync();
        await database.ExecuteAsync("CREATE TABLE [dbo].[VendorInvoices] ([Id] int NOT NULL CONSTRAINT [PK_TestVendorInvoices] PRIMARY KEY, [Marker] nvarchar(40) NOT NULL); INSERT INTO [dbo].[VendorInvoices] VALUES (42, N'preserve-me');");

        await database.ExecuteAsync(GetProjectSql(up: true).First());
        (await database.ScalarAsync<string>("SELECT [Marker] FROM [dbo].[VendorInvoice] WHERE [Id]=42;"))
            .Should().Be("preserve-me");

        var downSql = GetProjectSql(up: false);
        await database.ExecuteAsync(downSql.First());
        await database.ExecuteAsync(downSql.Last());

        (await database.ScalarAsync<string>("SELECT [Marker] FROM [dbo].[VendorInvoices] WHERE [Id]=42;"))
            .Should().Be("preserve-me");
        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE [name]=N'VendorInvoice';"))
            .Should().Be(0);
    }

    [FullSqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task SharedChain_CrmAndProjectForwardDowngradeRestoreImmediatePredecessors()
    {
        await using var database = await DisposableSqlDatabase.CreateAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(database.ConnectionString, sql => sql.CommandTimeout(600))
            .Options;
        await using var context = new ApplicationDbContext(options, Guid.NewGuid());

        await MaterializeSupportedBaselineAsync(context, database);
        var migrator = context.GetService<IMigrator>();

        await migrator.MigrateAsync(CrmPromotion);
        await AssertTableSetAsync(database, ["Leads", "Opportunities", "Quotes", "QuoteLineItems", "CrmActivities"],
            ["Lead", "Opportunity", "Quote", "QuoteLineItem", "Activities"]);

        await migrator.MigrateAsync(CrmCampaign);
        await AssertTableSetAsync(database,
            ["Lead", "Opportunity", "Quote", "QuoteLineItem", "Leads", "Opportunities", "Quotes", "QuoteLineItems", "Activities", "Campaigns", "CampaignMembers"],
            ["CrmActivities"]);

        await migrator.MigrateAsync(CrmSales);
        await AssertTableSetAsync(database,
            ["Lead", "Opportunity", "Quote", "QuoteLineItem", "Leads", "Opportunities", "Quotes", "QuoteLineItems", "Activities"],
            ["Campaigns", "CampaignMembers", "CrmActivities"]);

        await migrator.MigrateAsync(ProjectFoundation);
        await AssertTableSetAsync(database, ["VendorInvoice", "ProjectPackages", "ProjectBoqItems"], ["VendorInvoices"]);

        await migrator.MigrateAsync(ProjectPredecessor);
        await AssertTableSetAsync(database, ["VendorInvoices"], ["VendorInvoice", "ProjectPackages", "ProjectBoqItems"]);
    }

    private static async Task AssertTableSetAsync(
        DisposableSqlDatabase database,
        IReadOnlyCollection<string> present,
        IReadOnlyCollection<string> absent)
    {
        var names = string.Join(',', present.Concat(absent).Select(name => $"N'{name.Replace("'", "''")}'"));
        var actual = await database.QueryNamesAsync($"SELECT [name] FROM sys.tables WHERE [name] IN ({names});");
        actual.Should().Contain(present);
        actual.Should().NotContain(absent);
    }

    private static async Task MaterializeSupportedBaselineAsync(ApplicationDbContext context, DisposableSqlDatabase database)
    {
        var sqlGenerator = context.GetService<IMigrationsSqlGenerator>();
        var baseline = new InitialBaseline();
        foreach (var command in sqlGenerator.Generate(baseline.UpOperations, context.Model))
            await database.ExecuteAsync(command.CommandText);

        var history = context.GetService<IHistoryRepository>();
        await database.ExecuteAsync(history.GetCreateIfNotExistsScript());
        var version = typeof(DbContext).Assembly.GetName().Version?.ToString(3) ?? "8.0.0";
        foreach (var migrationId in context.GetService<IMigrationsAssembly>().Migrations.Keys
                     .Where(id => string.CompareOrdinal(id, InitialBaseline) <= 0)
                     .OrderBy(id => id, StringComparer.Ordinal))
            await database.ExecuteAsync(history.GetInsertScript(new HistoryRow(migrationId, version)));
    }

    private static string GetCrmCompatibilitySql() => new ExposedCrmMigration().UpSql().First();

    private static IReadOnlyList<string> GetProjectSql(bool up) =>
        (up ? new ExposedProjectMigration().UpSql() : new ExposedProjectMigration().DownSql());

    private const string CreateRedundantCrmGraphSql = """
        CREATE TABLE [dbo].[Leads] ([Id] int NOT NULL CONSTRAINT [PK_TestLeads] PRIMARY KEY);
        CREATE TABLE [dbo].[Opportunities] ([Id] int NOT NULL CONSTRAINT [PK_TestOpportunities] PRIMARY KEY);
        CREATE TABLE [dbo].[Activities] ([Id] int NOT NULL CONSTRAINT [PK_TestActivities] PRIMARY KEY);
        CREATE TABLE [dbo].[Quotes] ([Id] int NOT NULL CONSTRAINT [PK_TestQuotes] PRIMARY KEY);
        CREATE TABLE [dbo].[QuoteLineItems] ([Id] int NOT NULL CONSTRAINT [PK_TestQuoteLineItems] PRIMARY KEY);
        CREATE TABLE [dbo].[Campaigns] ([Id] int NOT NULL CONSTRAINT [PK_TestCampaigns] PRIMARY KEY);
        CREATE TABLE [dbo].[CampaignMembers] ([Id] int NOT NULL CONSTRAINT [PK_TestCampaignMembers] PRIMARY KEY);
        """;

    private sealed class ExposedCrmMigration : AddCrmEntities
    {
        public IReadOnlyList<string> UpSql()
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            Up(builder);
            return builder.Operations.OfType<SqlOperation>().Select(operation => operation.Sql).ToArray();
        }
    }

    private sealed class ExposedProjectMigration : AddProjectPackageBoqFoundation
    {
        public IReadOnlyList<string> UpSql()
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            Up(builder);
            return builder.Operations.OfType<SqlOperation>().Select(operation => operation.Sql).ToArray();
        }

        public IReadOnlyList<string> DownSql()
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            Down(builder);
            return builder.Operations.OfType<SqlOperation>().Select(operation => operation.Sql).ToArray();
        }
    }

    private sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")))
                Skip = "Set RHEMA_TEST_SQLSERVER to run the disposable migration compatibility gate.";
        }
    }

    private sealed class FullSqlServerFactAttribute : FactAttribute
    {
        public FullSqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")) ||
                string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER_FULL_MIGRATIONS")))
                Skip = "Set RHEMA_TEST_SQLSERVER and RHEMA_TEST_SQLSERVER_FULL_MIGRATIONS to run the full chain gate.";
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
            var databaseName = $"RHEMAERP_GL_REHEARSAL_COMPAT_{Guid.NewGuid():N}";
            if (!databaseName.StartsWith("RHEMAERP_GL_REHEARSAL_", StringComparison.Ordinal))
                throw new InvalidOperationException("Disposable database prefix assertion failed.");

            var master = new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = "master", TrustServerCertificate = true };
            var target = new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = databaseName, TrustServerCertificate = true };
            Console.WriteLine($"[CREATE] SQL Server: {target.DataSource}; Database: {databaseName}");

            var result = new DisposableSqlDatabase(databaseName, target.ConnectionString, master.ConnectionString);
            await using var connection = new SqlConnection(result._masterConnectionString);
            await connection.OpenAsync();
            await ExecuteAsync(connection, $"CREATE DATABASE [{databaseName}];");
            return result;
        }

        public async Task ExecuteAsync(string sql)
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await ExecuteAsync(connection, sql);
        }

        public async Task<T> ScalarAsync<T>(string sql)
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = 600;
            return (T)(await command.ExecuteScalarAsync())!;
        }

        public async Task<IReadOnlyList<string>> QueryNamesAsync(string sql)
        {
            var values = new List<string>();
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = 600;
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) values.Add(reader.GetString(0));
            return values;
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
            if (!_databaseName.StartsWith("RHEMAERP_GL_REHEARSAL_", StringComparison.Ordinal))
                throw new InvalidOperationException("Disposable database prefix assertion failed before drop.");
            var server = new SqlConnectionStringBuilder(_masterConnectionString).DataSource;
            Console.WriteLine($"[DROP] SQL Server: {server}; Database: {_databaseName}");
            await using var connection = new SqlConnection(_masterConnectionString);
            await connection.OpenAsync();
            await ExecuteAsync(connection,
                $"IF DB_ID(N'{_databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_databaseName}]; END;");
        }
    }
}
