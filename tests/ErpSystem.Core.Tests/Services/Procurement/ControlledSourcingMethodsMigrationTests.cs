using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ControlledSourcingMethodsMigrationTests
{
    [Fact]
    public void MigrationExtendsOnlyTheExistingGovernedMethodFamilies()
    {
        var sql = string.Join(Environment.NewLine, Operations().Select(operation => operation.Sql));

        sql.Should().Contain("[Method] IN (1, 2, 7, 8)");
        sql.Should().Contain("[Method] IN (3,4,5)");
        sql.Should().Contain("sc.[SelectedMethod] NOT IN (1, 2, 7, 8)");
        sql.Should().Contain("THROW 51109");
        sql.Should().NotContain("DISABLE TRIGGER");
    }

    [SqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task MigrationAppliesToDisposableSqlServerAndPreservesFailClosedTrigger()
    {
        var baseConnection = Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")!;
        var databaseName = $"RhemaERP_ControlledMethods_{Guid.NewGuid():N}";
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

        await using var master = new SqlConnection(masterBuilder.ConnectionString);
        await master.OpenAsync();
        try
        {
            await ExecuteAsync(master, $"CREATE DATABASE [{databaseName}];");
            await using var database = new SqlConnection(databaseBuilder.ConnectionString);
            await database.OpenAsync();
            await ExecuteAsync(database, MinimalSchemaSql);
            foreach (var operation in Operations())
                await ExecuteAsync(database, operation.Sql);

            var tenderConstraint = await ScalarAsync(database,
                "SELECT definition FROM sys.check_constraints WHERE name=N'CK_ProcurementTenderControls_State';");
            var exceptionalConstraint = await ScalarAsync(database,
                "SELECT definition FROM sys.check_constraints WHERE name=N'CK_ProcurementExceptionalSourcingControls_Core';");
            var trigger = await ScalarAsync(database,
                "SELECT OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_ProcurementTenderControls_Lifecycle')); ");

            tenderConstraint.Should().ContainAll("[Method]=(1)", "[Method]=(2)", "[Method]=(7)", "[Method]=(8)");
            exceptionalConstraint.Should().ContainAll("[Method]=(3)", "[Method]=(4)", "[Method]=(5)");
            trigger.Should().Contain("sc.[SelectedMethod] NOT IN (1, 2, 7, 8)");
        }
        finally
        {
            await ExecuteAsync(master,
                $"IF DB_ID(N'{databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]; END;");
        }
    }

    private static IReadOnlyList<SqlOperation> Operations()
    {
        var migration = new ExtendControlledSourcingMethods();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);
        return builder.Operations.OfType<SqlOperation>().ToList();
    }

    private static async Task ExecuteAsync(SqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string> ScalarAsync(SqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (await command.ExecuteScalarAsync())?.ToString() ?? string.Empty;
    }

    private sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")))
                Skip = "Set RHEMA_TEST_SQLSERVER to run the disposable controlled-method migration gate.";
        }
    }

    private const string MinimalSchemaSql =
        """
        CREATE TABLE dbo.ProcurementSourcingCases
        (
            Id uniqueidentifier NOT NULL PRIMARY KEY,
            SelectedMethod int NOT NULL
        );
        CREATE TABLE dbo.ProcurementTenderControls
        (
            Id uniqueidentifier NOT NULL PRIMARY KEY,
            SourcingCaseId uniqueidentifier NOT NULL,
            Method int NOT NULL,
            Status int NOT NULL,
            DocumentFee decimal(18,2) NOT NULL,
            OpeningScheduledAtUtc datetime2 NOT NULL,
            SubmissionDeadlineUtc datetime2 NOT NULL,
            IntegrityHash nvarchar(64) NOT NULL,
            LifecycleSnapshotJson nvarchar(max) NOT NULL,
            CONSTRAINT CK_ProcurementTenderControls_State CHECK
                (Method IN (1,2) AND Status BETWEEN 0 AND 9 AND DocumentFee >= 0
                 AND OpeningScheduledAtUtc >= SubmissionDeadlineUtc
                 AND LEN(IntegrityHash)=64 AND ISJSON(LifecycleSnapshotJson)=1)
        );
        CREATE TABLE dbo.ProcurementExceptionalSourcingControls
        (
            Id uniqueidentifier NOT NULL PRIMARY KEY,
            Method int NOT NULL,
            Status int NOT NULL,
            SupplierSnapshotJson nvarchar(max) NOT NULL,
            EvidenceChecklistJson nvarchar(max) NOT NULL,
            ApprovalActorsJson nvarchar(max) NOT NULL,
            IntegrityHash nvarchar(64) NOT NULL,
            CONSTRAINT CK_ProcurementExceptionalSourcingControls_Core CHECK
                (Method IN (3,4) AND Status BETWEEN 0 AND 9
                 AND ISJSON(SupplierSnapshotJson)=1 AND ISJSON(EvidenceChecklistJson)=1
                 AND ISJSON(ApprovalActorsJson)=1 AND LEN(IntegrityHash)=64)
        );
        EXEC(N'CREATE TRIGGER dbo.TR_ProcurementTenderControls_Lifecycle
          ON dbo.ProcurementTenderControls AFTER INSERT, UPDATE AS
          BEGIN
            SET NOCOUNT ON;
            IF EXISTS (SELECT 1 FROM inserted i JOIN dbo.ProcurementSourcingCases sc
                       ON sc.Id=i.SourcingCaseId WHERE sc.[SelectedMethod] NOT IN (1, 2))
              THROW 51100, ''Unsupported controlled method.'', 1;
          END');
        """;
}
