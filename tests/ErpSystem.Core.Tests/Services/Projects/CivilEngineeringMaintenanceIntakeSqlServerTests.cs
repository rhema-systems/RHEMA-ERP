using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

/// <summary>
/// Opt-in release proof for the real SQL Server CIV-0301 constraints and triggers.
/// RHEMA_TEST_SQLSERVER must be a disposable-database-capable SQL connection.
/// </summary>
public sealed class CivilEngineeringMaintenanceIntakeSqlServerTests
{
    [SqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task Intake_lineage_lifecycle_and_history_are_governed_on_sql_server()
    {
        var baseConnection = Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")!;
        var databaseName = $"RhemaERP_CivilMaintenance_{Guid.NewGuid():N}";
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

            var tenant = Guid.NewGuid();
            var foreignTenant = Guid.NewGuid();
            var project = Guid.NewGuid();
            var asset = Guid.NewGuid();
            var requester = Guid.NewGuid();
            var priority = Guid.NewGuid();
            var record = Guid.NewGuid();
            var version = Guid.NewGuid();
            var profile = Guid.NewGuid();
            var decision = Guid.NewGuid();
            var entityType = Guid.NewGuid();
            var workflow = Guid.NewGuid();
            var step = Guid.NewGuid();
            var template = Guid.NewGuid();
            var intake = Guid.NewGuid();
            var revision = Guid.NewGuid();

            await SeedLineageAsync(database, tenant, project, asset, requester, priority, record, version, profile, decision, entityType, workflow, step, template);
            await ExecuteAsync(database, InsertIntakeSql(intake, tenant, project, asset, requester, priority, record, version, profile, decision, workflow, template));

            var invalidLifecycle = () => ExecuteAsync(database, $"UPDATE dbo.CivilEngineeringMaintenanceIntakes SET Status='Assessed' WHERE Id='{intake}';");
            (await invalidLifecycle.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(52203);

            await ExecuteAsync(database, $"INSERT dbo.Tenants(Id) VALUES ('{foreignTenant}');");
            var foreign = Guid.NewGuid();
            var badTenant = () => ExecuteAsync(database, InsertIntakeSql(foreign, foreignTenant, project, asset, requester, priority, record, version, profile, decision, workflow, template));
            (await badTenant.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(52200);

            await ExecuteAsync(database, $$"""
                INSERT dbo.CivilEngineeringMaintenanceIntakeRevisions
                    (Id,IntakeId,Action,ActorUserId,ActorName,CorrelationId,AfterJson,CreatedAt,TenantId,IsDeleted)
                VALUES ('{{revision}}','{{intake}}','CIVIL_WORK_INTAKE_CREATED','{{requester}}',N'Requester',N'civil-maintenance-sql',N'{"state":"logged"}',SYSUTCDATETIME(),'{{tenant}}',0);
                """);
            var alterHistory = () => ExecuteAsync(database, $"UPDATE dbo.CivilEngineeringMaintenanceIntakeRevisions SET Action=N'changed' WHERE Id='{revision}';");
            (await alterHistory.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(52205);
        }
        finally
        {
            await ExecuteAsync(master, $"IF DB_ID(N'{databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]; END;");
        }
    }

    private static IReadOnlyList<SqlOperation> Operations()
    {
        var migration = new AddCivilEngineeringMaintenanceIntakes();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(migration, [builder]);
        return builder.Operations.OfType<SqlOperation>().ToList();
    }

    private static async Task SeedLineageAsync(SqlConnection connection, Guid tenant, Guid project, Guid asset, Guid requester, Guid priority, Guid record, Guid version, Guid profile, Guid decision, Guid entityType, Guid workflow, Guid step, Guid template)
    {
        await ExecuteAsync(connection, $$"""
            INSERT dbo.Tenants(Id) VALUES ('{{tenant}}');
            INSERT dbo.Projects(Id,TenantId,IsDeleted) VALUES ('{{project}}','{{tenant}}',0);
            INSERT dbo.MaintenanceAssets(Id,TenantId,IsDeleted,Status) VALUES ('{{asset}}','{{tenant}}',0,0);
            INSERT dbo.Users(Id,TenantId,IsActive) VALUES ('{{requester}}','{{tenant}}',1);
            INSERT dbo.PriorityLevels(Id,TenantId,IsDeleted,IsActive) VALUES ('{{priority}}','{{tenant}}',0,1);
            INSERT dbo.CentralDocumentMetadataTemplates(Id,TenantId,IsDeleted,IsActive,PublishedAt,TemplateCode) VALUES ('{{template}}','{{tenant}}',0,1,SYSUTCDATETIME(),N'CIV-MAINTENANCE-EVIDENCE');
            INSERT dbo.CentralDocumentRecords(Id,TenantId,IsDeleted,LifecycleStatus,VersionStatus,CurrentVersion,MetadataTemplateCode) VALUES ('{{record}}','{{tenant}}',0,N'Active',N'Published',N'1',N'CIV-MAINTENANCE-EVIDENCE');
            INSERT dbo.CentralDocumentVersions(Id,DocumentRecordId,TenantId,IsDeleted,Status,PublishedAt,VersionNumber) VALUES ('{{version}}','{{record}}','{{tenant}}',0,N'Published',SYSUTCDATETIME(),N'1');
            INSERT dbo.WorkflowEntityTypes(Id,TenantId,IsDeleted,IsActive,Code) VALUES ('{{entityType}}','{{tenant}}',0,1,N'PROJECT_MAINTENANCE_ASSESSMENT');
            INSERT dbo.WorkflowDefinitions(Id,TenantId,IsDeleted,IsActive,LifecycleStatus,EntityTypeId) VALUES ('{{workflow}}','{{tenant}}',0,1,1,'{{entityType}}');
            INSERT dbo.WorkflowSteps(Id,WorkflowDefinitionId,TenantId,IsDeleted) VALUES ('{{step}}','{{workflow}}','{{tenant}}',0);
            INSERT dbo.CivilEngineeringConfigurationProfiles(Id,TenantId,IsDeleted,LifecycleStatus,EffectiveFrom,EffectiveTo) VALUES ('{{profile}}','{{tenant}}',0,1,DATEADD(day,-1,SYSUTCDATETIME()),NULL);
            INSERT dbo.CivilEngineeringConfigurationDecisions(Id,TenantId,ProfileId,ConfigurationKey,IsDeleted,Status,ApprovalStatus,EvidenceStatus,ValueJson) VALUES ('{{decision}}','{{tenant}}','{{profile}}',N'CIV-CFG-007',0,2,1,2,N'{"workflowDefinitionId":"{{workflow}}","metadataTemplateId":"{{template}}"}');
            """);
    }

    private static string InsertIntakeSql(Guid intake, Guid tenant, Guid project, Guid asset, Guid requester, Guid priority, Guid record, Guid version, Guid profile, Guid decision, Guid workflow, Guid template) => $$"""
        INSERT dbo.CivilEngineeringMaintenanceIntakes
            (Id,IntakeNumber,WorkClassification,Source,Urgency,Title,Description,ProjectId,MaintenanceAssetId,EstateManagedAssetId,MaintenanceScheduleId,HelpdeskTicketId,
             RequesterUserId,PriorityLevelId,CentralDocumentRecordId,CentralDocumentVersionId,ConfigurationProfileId,ConfigurationDecisionId,WorkflowDefinitionId,EvidenceMetadataTemplateId,
             EvidenceMetadataTemplateCodeSnapshot,PolicyHash,Status,ClientRequestId,RequestHash,CorrelationId,CreatedAt,TenantId,IsDeleted)
        VALUES
            ('{{intake}}',N'CIV-MNT-2026-00001',3,1,1,N'Breakdown intake',N'Controlled physical-asset maintenance intake.', '{{project}}','{{asset}}',NULL,NULL,NULL,
             '{{requester}}','{{priority}}','{{record}}','{{version}}','{{profile}}','{{decision}}','{{workflow}}','{{template}}',N'CIV-MAINTENANCE-EVIDENCE',REPLICATE('A',64),N'Logged',NEWID(),REPLICATE('B',64),N'civil-maintenance-sql',SYSUTCDATETIME(),'{{tenant}}',0);
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
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")))
                Skip = "Set RHEMA_TEST_SQLSERVER to run the disposable Civil maintenance-intake SQL Server gate.";
        }
    }

    private const string MinimalSchemaSql = """
        CREATE TABLE dbo.Tenants (Id uniqueidentifier NOT NULL PRIMARY KEY);
        CREATE TABLE dbo.Projects (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL);
        CREATE TABLE dbo.MaintenanceAssets (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL, Status int NOT NULL);
        CREATE TABLE dbo.EstateManagedAssets (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL);
        CREATE TABLE dbo.MaintenanceSchedules (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL, IsActive bit NOT NULL, AssetId uniqueidentifier NULL);
        CREATE TABLE dbo.EhcTickets (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL, TicketType int NOT NULL, Status int NOT NULL);
        CREATE TABLE dbo.Users (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsActive bit NOT NULL);
        CREATE TABLE dbo.PriorityLevels (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL, IsActive bit NOT NULL);
        CREATE TABLE dbo.CentralDocumentRecords (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL, LifecycleStatus nvarchar(40) NOT NULL, VersionStatus nvarchar(40) NOT NULL, CurrentVersion nvarchar(40) NOT NULL, MetadataTemplateCode nvarchar(100) NOT NULL);
        CREATE TABLE dbo.CentralDocumentVersions (Id uniqueidentifier NOT NULL PRIMARY KEY, DocumentRecordId uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL, Status nvarchar(40) NOT NULL, PublishedAt datetime2 NULL, VersionNumber nvarchar(40) NOT NULL);
        CREATE TABLE dbo.CivilEngineeringConfigurationProfiles (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL, LifecycleStatus int NOT NULL, EffectiveFrom datetime2 NOT NULL, EffectiveTo datetime2 NULL);
        CREATE TABLE dbo.CivilEngineeringConfigurationDecisions (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, ProfileId uniqueidentifier NOT NULL, ConfigurationKey nvarchar(100) NOT NULL, IsDeleted bit NOT NULL, Status int NOT NULL, ApprovalStatus int NOT NULL, EvidenceStatus int NOT NULL, ValueJson nvarchar(max) NOT NULL);
        CREATE TABLE dbo.WorkflowEntityTypes (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL, IsActive bit NOT NULL, Code nvarchar(100) NOT NULL);
        CREATE TABLE dbo.WorkflowDefinitions (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL, IsActive bit NOT NULL, LifecycleStatus int NOT NULL, EntityTypeId uniqueidentifier NOT NULL);
        CREATE TABLE dbo.WorkflowSteps (Id uniqueidentifier NOT NULL PRIMARY KEY, WorkflowDefinitionId uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL);
        CREATE TABLE dbo.CentralDocumentMetadataTemplates (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL, IsActive bit NOT NULL, PublishedAt datetime2 NULL, TemplateCode nvarchar(100) NOT NULL);
        """;
}
