using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringMaintenanceExecutionLinkSqlServerTests
{
    [SqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task Execution_link_migration_enforces_awarded_scope_asset_lineage_lifecycle_and_history()
    {
        var baseConnection = Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")!;
        var databaseName = $"RhemaERP_CivilExecution_{Guid.NewGuid():N}";
        var masterConnection = new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = "master", TrustServerCertificate = true }.ConnectionString;
        var databaseConnection = new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = databaseName, TrustServerCertificate = true }.ConnectionString;
        await using var master = new SqlConnection(masterConnection);
        await master.OpenAsync();

        try
        {
            await ExecuteAsync(master, $"CREATE DATABASE [{databaseName}];");
            await using var database = new SqlConnection(databaseConnection);
            await database.OpenAsync();
            await ExecuteAsync(database, MinimalSchemaSql);
            foreach (var operation in Operations()) await ExecuteAsync(database, operation.Sql);

            var tenant = Guid.NewGuid(); var foreignTenant = Guid.NewGuid(); var actor = Guid.NewGuid(); var project = Guid.NewGuid();
            var asset = Guid.NewGuid(); var intake = Guid.NewGuid(); var assessment = Guid.NewGuid(); var handoff = Guid.NewGuid();
            var jobCard = Guid.NewGuid(); var replacementJobCard = Guid.NewGuid(); var link = Guid.NewGuid(); var revision = Guid.NewGuid();
            await SeedAsync(database, tenant, actor, project, asset, intake, assessment, handoff, jobCard, replacementJobCard);
            await ExecuteAsync(database, InsertLinkSql(link, tenant, handoff, project, asset, jobCard));
            await ExecuteAsync(database, $"UPDATE dbo.CivilEngineeringMaintenanceExecutionLinks SET LastRevalidatedAt=SYSUTCDATETIME(), LastOwnerStatusSummary=N'Job card remains pending', LastMutationClientRequestId=NEWID(), LastMutationRequestHash=REPLICATE('C',64), CorrelationId=N'civil-execution-refresh' WHERE Id='{link}';");

            var invalidTransition = () => ExecuteAsync(database, $"UPDATE dbo.CivilEngineeringMaintenanceExecutionLinks SET Stage=N'Completed',Status=N'Completed' WHERE Id='{link}';");
            (await invalidTransition.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(52233);
            var frozenLink = () => ExecuteAsync(database, $"UPDATE dbo.CivilEngineeringMaintenanceExecutionLinks SET JobCardId='{replacementJobCard}' WHERE Id='{link}';");
            (await frozenLink.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(52232);

            await ExecuteAsync(database, $"INSERT dbo.Tenants(Id) VALUES ('{foreignTenant}');");
            var crossTenant = () => ExecuteAsync(database, InsertLinkSql(Guid.NewGuid(), foreignTenant, handoff, project, asset, jobCard));
            (await crossTenant.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(52230);

            await ExecuteAsync(database, $$"""
                INSERT dbo.CivilEngineeringMaintenanceExecutionLinkRevisions
                    (Id,ExecutionLinkId,Action,ToStage,ActorUserId,ActorName,CorrelationId,AfterJson,CreatedAt,TenantId,IsDeleted)
                VALUES ('{{revision}}','{{link}}',N'UpdateCivilInterfaceStatus',N'AwaitingJobCardApproval','{{actor}}',N'HOD',N'civil-execution-sql',N'{}',SYSUTCDATETIME(),'{{tenant}}',0);
                """);
            var mutateRevision = () => ExecuteAsync(database, $"UPDATE dbo.CivilEngineeringMaintenanceExecutionLinkRevisions SET Action=N'changed' WHERE Id='{revision}';");
            (await mutateRevision.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(52236);
        }
        finally
        {
            await ExecuteAsync(master, $"IF DB_ID(N'{databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]; END;");
        }
    }

    private static IReadOnlyList<SqlOperation> Operations()
    {
        var migration = new AddCivilEngineeringMaintenanceExecutionLinks();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(migration, [builder]);
        return builder.Operations.OfType<SqlOperation>().ToList();
    }

    private static Task SeedAsync(SqlConnection connection, Guid tenant, Guid actor, Guid project, Guid asset, Guid intake, Guid assessment, Guid handoff, Guid jobCard, Guid replacementJobCard) => ExecuteAsync(connection, $$"""
        INSERT dbo.Tenants(Id) VALUES ('{{tenant}}');
        INSERT dbo.Users(Id,TenantId,IsActive) VALUES ('{{actor}}','{{tenant}}',1);
        INSERT dbo.Projects(Id,TenantId,IsDeleted) VALUES ('{{project}}','{{tenant}}',0);
        INSERT dbo.MaintenanceAssets(Id,TenantId,IsDeleted) VALUES ('{{asset}}','{{tenant}}',0);
        INSERT dbo.CivilEngineeringMaintenanceIntakes(Id,TenantId,IsDeleted,MaintenanceAssetId) VALUES ('{{intake}}','{{tenant}}',0,'{{asset}}');
        INSERT dbo.CivilEngineeringMaintenanceAssessments(Id,TenantId,IsDeleted,IntakeId) VALUES ('{{assessment}}','{{tenant}}',0,'{{intake}}');
        INSERT dbo.CivilEngineeringMaintenanceCostingHandoffs(Id,TenantId,IsDeleted,AssessmentId,ProjectId,Stage,Status,ApprovalStatus) VALUES ('{{handoff}}','{{tenant}}',0,'{{assessment}}','{{project}}',N'Awarded',N'Awarded',N'Approved');
        INSERT dbo.JobCard(Id,TenantId,IsDeleted,AssetId,JobCardStatus,ApprovalStatus,GeneratedWorkOrderId) VALUES ('{{jobCard}}','{{tenant}}',0,'{{asset}}',N'Draft',N'NotStarted',NULL),('{{replacementJobCard}}','{{tenant}}',0,'{{asset}}',N'Draft',N'NotStarted',NULL);
        """);

    private static string InsertLinkSql(Guid link, Guid tenant, Guid handoff, Guid project, Guid asset, Guid jobCard) => $$"""
        INSERT dbo.CivilEngineeringMaintenanceExecutionLinks
            (Id,HandoffId,ProjectId,MaintenanceAssetId,JobCardId,LinkMode,Stage,Status,LastOwnerStatusSummary,ClientRequestId,RequestHash,CorrelationId,CreatedAt,TenantId,IsDeleted)
        VALUES ('{{link}}','{{handoff}}','{{project}}','{{asset}}','{{jobCard}}',N'LinkExisting',N'AwaitingJobCardApproval',N'Pending',N'Job card pending',NEWID(),REPLICATE('B',64),N'civil-execution-sql',SYSUTCDATETIME(),'{{tenant}}',0);
        """;

    private static Task ExecuteAsync(SqlConnection connection, string sql)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteNonQueryAsync();
    }

    private sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER"))) Skip = "Set RHEMA_TEST_SQLSERVER to run the disposable Civil execution-link SQL Server gate.";
        }
    }

    private const string MinimalSchemaSql = """
        CREATE TABLE dbo.Tenants (Id uniqueidentifier NOT NULL PRIMARY KEY);
        CREATE TABLE dbo.Users (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsActive bit NOT NULL);
        CREATE TABLE dbo.Projects (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL);
        CREATE TABLE dbo.MaintenanceAssets (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL);
        CREATE TABLE dbo.JobCard (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL, AssetId uniqueidentifier NOT NULL, JobCardStatus nvarchar(40) NOT NULL, ApprovalStatus nvarchar(40) NOT NULL, GeneratedWorkOrderId uniqueidentifier NULL);
        CREATE TABLE dbo.WorkOrders (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL, AssetId uniqueidentifier NOT NULL, JobCardId uniqueidentifier NULL, Status nvarchar(40) NOT NULL);
        CREATE TABLE dbo.CivilEngineeringMaintenanceIntakes (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL, MaintenanceAssetId uniqueidentifier NULL);
        CREATE TABLE dbo.CivilEngineeringMaintenanceAssessments (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL, IntakeId uniqueidentifier NOT NULL);
        CREATE TABLE dbo.CivilEngineeringMaintenanceCostingHandoffs (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL, AssessmentId uniqueidentifier NOT NULL, ProjectId uniqueidentifier NOT NULL, Stage nvarchar(40) NOT NULL, Status nvarchar(40) NOT NULL, ApprovalStatus nvarchar(40) NOT NULL);
        """;
}
