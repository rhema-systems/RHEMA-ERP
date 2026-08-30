using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringMaintenanceCompletionControlSqlServerTests
{
    [SqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task Completion_control_migration_enforces_tenant_lineage_forward_state_and_append_only_history()
    {
        var baseConnection = Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")!;
        var databaseName = $"RhemaERP_CivilCompletion_{Guid.NewGuid():N}";
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
            var tenant = Guid.NewGuid(); var foreignTenant = Guid.NewGuid(); var civilEngineer = Guid.NewGuid(); var sce = Guid.NewGuid(); var hod = Guid.NewGuid(); var project = Guid.NewGuid(); var asset = Guid.NewGuid(); var intake = Guid.NewGuid(); var assessment = Guid.NewGuid(); var handoff = Guid.NewGuid(); var jobCard = Guid.NewGuid(); var workOrder = Guid.NewGuid(); var replacementWorkOrder = Guid.NewGuid(); var link = Guid.NewGuid(); var control = Guid.NewGuid(); var revision = Guid.NewGuid();
            await SeedAsync(database, tenant, civilEngineer, sce, hod, project, asset, intake, assessment, handoff, jobCard, workOrder, replacementWorkOrder, link);
            await ExecuteAsync(database, InsertControlSql(control, tenant, link, project, asset, jobCard, workOrder, civilEngineer, sce, hod));
            await ExecuteAsync(database, $"UPDATE dbo.CivilEngineeringMaintenanceCompletionControls SET Stage=N'HodReview', SceReviewedById='{sce}', SceReviewedAt=SYSUTCDATETIME(), LastMutationClientRequestId=NEWID(), LastMutationRequestHash=REPLICATE('C',64) WHERE Id='{control}';");
            var invalidTransition = () => ExecuteAsync(database, $"UPDATE dbo.CivilEngineeringMaintenanceCompletionControls SET Stage=N'Closed', Status=N'Closed' WHERE Id='{control}';");
            (await invalidTransition.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(52244);
            var frozenSource = () => ExecuteAsync(database, $"UPDATE dbo.CivilEngineeringMaintenanceCompletionControls SET WorkOrderId='{replacementWorkOrder}' WHERE Id='{control}';");
            (await frozenSource.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(52242);
            await ExecuteAsync(database, $"INSERT dbo.CivilEngineeringMaintenanceCompletionRevisions(Id,CompletionControlId,Action,ToStage,ActorUserId,ActorName,CorrelationId,AfterJson,CreatedAt,TenantId,IsDeleted) VALUES ('{revision}','{control}',N'SubmitCivilCompletionReport',N'HodReview','{sce}',N'SCE',N'completion-sql',N'{{}}',SYSUTCDATETIME(),'{tenant}',0);");
            var mutateHistory = () => ExecuteAsync(database, $"UPDATE dbo.CivilEngineeringMaintenanceCompletionRevisions SET Action=N'changed' WHERE Id='{revision}';");
            (await mutateHistory.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(52247);
            await ExecuteAsync(database, $"INSERT dbo.Tenants(Id) VALUES ('{foreignTenant}');");
            var crossTenant = () => ExecuteAsync(database, InsertControlSql(Guid.NewGuid(), foreignTenant, link, project, asset, jobCard, workOrder, civilEngineer, sce, hod));
            (await crossTenant.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(52240);
        }
        finally
        {
            await ExecuteAsync(master, $"IF DB_ID(N'{databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]; END;");
        }
    }

    private static IReadOnlyList<SqlOperation> Operations()
    {
        var migration = new AddCivilEngineeringMaintenanceCompletionControls();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(migration, [builder]);
        return builder.Operations.OfType<SqlOperation>().ToList();
    }

    private static Task SeedAsync(SqlConnection connection, Guid tenant, Guid civilEngineer, Guid sce, Guid hod, Guid project, Guid asset, Guid intake, Guid assessment, Guid handoff, Guid jobCard, Guid workOrder, Guid replacementWorkOrder, Guid link) => ExecuteAsync(connection, $$"""
        INSERT dbo.Tenants(Id) VALUES ('{{tenant}}');
        INSERT dbo.Users(Id,TenantId,IsActive) VALUES ('{{civilEngineer}}','{{tenant}}',1),('{{sce}}','{{tenant}}',1),('{{hod}}','{{tenant}}',1);
        INSERT dbo.Projects(Id,TenantId,IsDeleted) VALUES ('{{project}}','{{tenant}}',0);
        INSERT dbo.MaintenanceAssets(Id,TenantId,IsDeleted) VALUES ('{{asset}}','{{tenant}}',0);
        INSERT dbo.CivilEngineeringMaintenanceIntakes(Id,TenantId,IsDeleted,MaintenanceAssetId) VALUES ('{{intake}}','{{tenant}}',0,'{{asset}}');
        INSERT dbo.CivilEngineeringMaintenanceAssessments(Id,TenantId,IsDeleted,IntakeId,CivilEngineerUserId,SupervisingCivilEngineerUserId,HodUserId) VALUES ('{{assessment}}','{{tenant}}',0,'{{intake}}','{{civilEngineer}}','{{sce}}','{{hod}}');
        INSERT dbo.CivilEngineeringMaintenanceCostingHandoffs(Id,TenantId,IsDeleted,AssessmentId,ProjectId,Stage,Status,ApprovalStatus) VALUES ('{{handoff}}','{{tenant}}',0,'{{assessment}}','{{project}}',N'Awarded',N'Awarded',N'Approved');
        INSERT dbo.JobCard(Id,TenantId,IsDeleted,AssetId) VALUES ('{{jobCard}}','{{tenant}}',0,'{{asset}}');
        INSERT dbo.WorkOrders(Id,TenantId,IsDeleted,AssetId,JobCardId,Status) VALUES ('{{workOrder}}','{{tenant}}',0,'{{asset}}','{{jobCard}}',N'Completed'),('{{replacementWorkOrder}}','{{tenant}}',0,'{{asset}}','{{jobCard}}',N'Completed');
        INSERT dbo.CivilEngineeringMaintenanceExecutionLinks(Id,TenantId,IsDeleted,HandoffId,ProjectId,MaintenanceAssetId,JobCardId,WorkOrderId,Stage,Status) VALUES ('{{link}}','{{tenant}}',0,'{{handoff}}','{{project}}','{{asset}}','{{jobCard}}','{{workOrder}}',N'Completed',N'Completed');
        """);

    private static string InsertControlSql(Guid control, Guid tenant, Guid link, Guid project, Guid asset, Guid jobCard, Guid workOrder, Guid civilEngineer, Guid sce, Guid hod) => $$"""
        INSERT dbo.CivilEngineeringMaintenanceCompletionControls
          (Id,ExecutionLinkId,ProjectId,MaintenanceAssetId,JobCardId,WorkOrderId,CivilEngineerUserId,SupervisingCivilEngineerUserId,HodUserId,Stage,Status,CompletionSummary,CompletionDocumentRecordId,CompletionDocumentVersionId,CompletionReportedAt,InspectionStatus,PaymentDirectionStatus,ClientRequestId,RequestHash,CorrelationId,CreatedAt,TenantId,IsDeleted)
        VALUES ('{{control}}','{{link}}','{{project}}','{{asset}}','{{jobCard}}','{{workOrder}}','{{civilEngineer}}','{{sce}}','{{hod}}',N'SceReview',N'Pending',N'Completed maintenance scope',NEWID(),NEWID(),SYSUTCDATETIME(),N'NotDirected',N'NotDirected',NEWID(),REPLICATE('B',64),N'completion-sql',SYSUTCDATETIME(),'{{tenant}}',0);
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
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER"))) Skip = "Set RHEMA_TEST_SQLSERVER to run the disposable Civil completion-control SQL Server gate.";
        }
    }

    private const string MinimalSchemaSql = """
        CREATE TABLE dbo.Tenants (Id uniqueidentifier NOT NULL PRIMARY KEY);
        CREATE TABLE dbo.Users (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsActive bit NOT NULL);
        CREATE TABLE dbo.Projects (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL);
        CREATE TABLE dbo.MaintenanceAssets (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL);
        CREATE TABLE dbo.JobCard (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL, AssetId uniqueidentifier NOT NULL);
        CREATE TABLE dbo.WorkOrders (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL, AssetId uniqueidentifier NOT NULL, JobCardId uniqueidentifier NULL, Status nvarchar(40) NOT NULL);
        CREATE TABLE dbo.CivilEngineeringMaintenanceIntakes (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL, MaintenanceAssetId uniqueidentifier NULL);
        CREATE TABLE dbo.CivilEngineeringMaintenanceAssessments (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL, IntakeId uniqueidentifier NOT NULL, CivilEngineerUserId uniqueidentifier NOT NULL, SupervisingCivilEngineerUserId uniqueidentifier NOT NULL, HodUserId uniqueidentifier NOT NULL);
        CREATE TABLE dbo.CivilEngineeringMaintenanceCostingHandoffs (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL, AssessmentId uniqueidentifier NOT NULL, ProjectId uniqueidentifier NOT NULL, Stage nvarchar(40) NOT NULL, Status nvarchar(40) NOT NULL, ApprovalStatus nvarchar(40) NOT NULL);
        CREATE TABLE dbo.CivilEngineeringMaintenanceExecutionLinks (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL, HandoffId uniqueidentifier NOT NULL, ProjectId uniqueidentifier NOT NULL, MaintenanceAssetId uniqueidentifier NOT NULL, JobCardId uniqueidentifier NULL, WorkOrderId uniqueidentifier NULL, Stage nvarchar(40) NOT NULL, Status nvarchar(40) NOT NULL);
        """;
}
