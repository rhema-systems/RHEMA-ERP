using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringMaintenanceAssessmentSqlServerTests
{
    [SqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task Assessment_lineage_stage_transitions_and_history_are_governed_on_sql_server()
    {
        var baseConnection = Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")!;
        var databaseName = $"RhemaERP_CivilAssessment_{Guid.NewGuid():N}";
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

            var tenant = Guid.NewGuid(); var foreignTenant = Guid.NewGuid(); var intake = Guid.NewGuid(); var hod = Guid.NewGuid(); var sce = Guid.NewGuid(); var engineer = Guid.NewGuid(); var category = Guid.NewGuid(); var record = Guid.NewGuid(); var version = Guid.NewGuid(); var assessment = Guid.NewGuid(); var revision = Guid.NewGuid();
            await SeedAsync(database, tenant, intake, hod, sce, engineer, category, record, version);
            await ExecuteAsync(database, $"INSERT dbo.Tenants(Id) VALUES ('{foreignTenant}');");
            await ExecuteAsync(database, InsertAssessmentSql(assessment, tenant, intake, hod, sce));

            var invalidStage = () => ExecuteAsync(database, $"UPDATE dbo.CivilEngineeringMaintenanceAssessments SET Stage='Approved',Status='Approved',ApprovalStatus='Approved' WHERE Id='{assessment}';");
            (await invalidStage.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(52213);

            await ExecuteAsync(database, $$"""UPDATE dbo.CivilEngineeringMaintenanceAssessments SET CivilEngineerUserId='{{engineer}}',CurrentAssigneeUserId='{{engineer}}',CurrentDueAt=DATEADD(day,1,SYSUTCDATETIME()),Stage='CivilEngineerAssessment' WHERE Id='{{assessment}}';""");
            await ExecuteAsync(database, $$"""UPDATE dbo.CivilEngineeringMaintenanceAssessments SET DefectCategoryId='{{category}}',SiteAssessment=N'Observed defect assessment',ScopeRecommendation=N'Controlled remedy scope',RemedyRecommendation=N'Controlled remedy',EstimatedCost=100,CentralDocumentRecordId='{{record}}',CentralDocumentVersionId='{{version}}',WorkflowInstanceId=NEWID(),CurrentAssigneeUserId='{{sce}}',Stage='SceAssessmentReview',Status='PendingApproval',ApprovalStatus='Pending' WHERE Id='{{assessment}}';""");
            var mutateSubmittedScope = () => ExecuteAsync(database, $"UPDATE dbo.CivilEngineeringMaintenanceAssessments SET ScopeRecommendation=N'changed' WHERE Id='{assessment}';");
            (await mutateSubmittedScope.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(52214);

            var foreign = () => ExecuteAsync(database, InsertAssessmentSql(Guid.NewGuid(), foreignTenant, intake, hod, sce));
            (await foreign.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(52210);

            await ExecuteAsync(database, $$"""INSERT dbo.CivilEngineeringMaintenanceAssessmentRevisions(Id,AssessmentId,Action,ToStage,ActorUserId,ActorName,CorrelationId,AfterJson,CreatedAt,TenantId,IsDeleted) VALUES ('{{revision}}','{{assessment}}',N'UpdateCivilWorkAssessment',N'SceAssessmentReview','{{sce}}',N'SCE',N'civil-assessment-sql',N'{}',SYSUTCDATETIME(),'{{tenant}}',0);""");
            var mutateRevision = () => ExecuteAsync(database, $"UPDATE dbo.CivilEngineeringMaintenanceAssessmentRevisions SET Action=N'changed' WHERE Id='{revision}';");
            (await mutateRevision.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(52217);
        }
        finally { await ExecuteAsync(master, $"IF DB_ID(N'{databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]; END;"); }
    }

    private static IReadOnlyList<SqlOperation> Operations()
    {
        var migration = new AddCivilEngineeringMaintenanceAssessments();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(migration, [builder]);
        return builder.Operations.OfType<SqlOperation>().ToList();
    }

    private static async Task SeedAsync(SqlConnection connection, Guid tenant, Guid intake, Guid hod, Guid sce, Guid engineer, Guid category, Guid record, Guid version) => await ExecuteAsync(connection, $$"""
        INSERT dbo.Tenants(Id) VALUES ('{{tenant}}');
        INSERT dbo.Users(Id,TenantId,IsActive) VALUES ('{{hod}}','{{tenant}}',1),('{{sce}}','{{tenant}}',1),('{{engineer}}','{{tenant}}',1);
        INSERT dbo.ProjectCatalogEntries(Id,TenantId,IsDeleted,IsActive,CatalogType) VALUES ('{{category}}','{{tenant}}',0,1,N'civil-defect-categories');
        INSERT dbo.CentralDocumentRecords(Id,TenantId,IsDeleted,LifecycleStatus,VersionStatus,CurrentVersion,MetadataTemplateCode) VALUES ('{{record}}','{{tenant}}',0,N'Active',N'Published',N'1',N'CIV-MAINTENANCE-EVIDENCE');
        INSERT dbo.CentralDocumentVersions(Id,DocumentRecordId,TenantId,IsDeleted,Status,PublishedAt,VersionNumber) VALUES ('{{version}}','{{record}}','{{tenant}}',0,N'Published',SYSUTCDATETIME(),N'1');
        INSERT dbo.CivilEngineeringMaintenanceIntakes(Id,TenantId,IsDeleted,Status,PolicyHash,EvidenceMetadataTemplateCodeSnapshot) VALUES ('{{intake}}','{{tenant}}',0,N'Logged',REPLICATE('A',64),N'CIV-MAINTENANCE-EVIDENCE');
        """);

    private static string InsertAssessmentSql(Guid id, Guid tenant, Guid intake, Guid hod, Guid sce) => $$"""
        INSERT dbo.CivilEngineeringMaintenanceAssessments(Id,IntakeId,HodUserId,SupervisingCivilEngineerUserId,CurrentAssigneeUserId,PolicyHash,Stage,Status,ApprovalStatus,ClientRequestId,RequestHash,CorrelationId,CreatedAt,TenantId,IsDeleted)
        VALUES ('{{id}}','{{intake}}','{{hod}}','{{sce}}','{{sce}}',REPLICATE('A',64),N'SceAssignment',N'InProgress',N'Draft',NEWID(),REPLICATE('B',64),N'civil-assessment-sql',SYSUTCDATETIME(),'{{tenant}}',0);
        """;
    private static Task ExecuteAsync(SqlConnection connection, string sql) { var command = connection.CreateCommand(); command.CommandText = sql; return command.ExecuteNonQueryAsync(); }
    private sealed class SqlServerFactAttribute : FactAttribute { public SqlServerFactAttribute() { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER"))) Skip = "Set RHEMA_TEST_SQLSERVER to run the disposable Civil maintenance-assessment SQL Server gate."; } }
    private const string MinimalSchemaSql = """
        CREATE TABLE dbo.Tenants (Id uniqueidentifier NOT NULL PRIMARY KEY);
        CREATE TABLE dbo.Users (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsActive bit NOT NULL);
        CREATE TABLE dbo.ProjectCatalogEntries (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL, IsActive bit NOT NULL, CatalogType nvarchar(100) NOT NULL);
        CREATE TABLE dbo.CentralDocumentRecords (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL, LifecycleStatus nvarchar(40) NOT NULL, VersionStatus nvarchar(40) NOT NULL, CurrentVersion nvarchar(40) NOT NULL, MetadataTemplateCode nvarchar(100) NOT NULL);
        CREATE TABLE dbo.CentralDocumentVersions (Id uniqueidentifier NOT NULL PRIMARY KEY, DocumentRecordId uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL, Status nvarchar(40) NOT NULL, PublishedAt datetime2 NULL, VersionNumber nvarchar(40) NOT NULL);
        CREATE TABLE dbo.CivilEngineeringMaintenanceIntakes (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL, Status varchar(40) NOT NULL, PolicyHash varchar(64) NOT NULL, EvidenceMetadataTemplateCodeSnapshot varchar(80) NOT NULL);
        """;
}
