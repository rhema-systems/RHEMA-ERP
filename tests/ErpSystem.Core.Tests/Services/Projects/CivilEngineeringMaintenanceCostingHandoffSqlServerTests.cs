using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

/// <summary>
/// Disposable SQL Server proof for the Civil handoff overlay. The owner records remain
/// in QS, Projects/Finance and Procurement; this gate proves their references cannot
/// be crossed between tenants or mutated after handoff creation.
/// </summary>
public sealed class CivilEngineeringMaintenanceCostingHandoffSqlServerTests
{
    [SqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task Costing_handoff_lineage_lifecycle_refresh_and_history_are_governed_on_sql_server()
    {
        var baseConnection = Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")!;
        var databaseName = $"RhemaERP_CivilCosting_{Guid.NewGuid():N}";
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
            var actor = Guid.NewGuid();
            var intake = Guid.NewGuid();
            var assessment = Guid.NewGuid();
            var project = Guid.NewGuid();
            var estimate = Guid.NewGuid();
            var secondEstimate = Guid.NewGuid();
            var budget = Guid.NewGuid();
            var requisition = Guid.NewGuid();
            var contract = Guid.NewGuid();
            var profile = Guid.NewGuid();
            var decision = Guid.NewGuid();
            var costingWorkflow = Guid.NewGuid();
            var contractorWorkflow = Guid.NewGuid();
            var handoff = Guid.NewGuid();
            var revision = Guid.NewGuid();

            await SeedAsync(database, tenant, actor, intake, assessment, project, estimate, secondEstimate, budget, requisition, contract, profile, decision, costingWorkflow, contractorWorkflow);
            await ExecuteAsync(database, InsertHandoffSql(handoff, tenant, assessment, project, estimate, budget, requisition, contract, profile, decision, costingWorkflow, contractorWorkflow));

            await ExecuteAsync(database, $"UPDATE dbo.CivilEngineeringMaintenanceCostingHandoffs SET LastRevalidatedAt=SYSUTCDATETIME(), LastRevalidationSummary=N'Current owner links remain valid', LastMutationClientRequestId=NEWID(), LastMutationRequestHash=REPLICATE('C',64), CorrelationId=N'civil-costing-refresh' WHERE Id='{handoff}';");

            var invalidTransition = () => ExecuteAsync(database, $"UPDATE dbo.CivilEngineeringMaintenanceCostingHandoffs SET Stage=N'Awarded',Status=N'Awarded',ApprovalStatus=N'Approved',WorkflowInstanceId=NEWID() WHERE Id='{handoff}';");
            (await invalidTransition.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(52223);

            var frozenOwnerReference = () => ExecuteAsync(database, $"UPDATE dbo.CivilEngineeringMaintenanceCostingHandoffs SET QuantitySurveyEstimateVersionId='{secondEstimate}' WHERE Id='{handoff}';");
            (await frozenOwnerReference.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(52222);

            await ExecuteAsync(database, $"INSERT dbo.Tenants(Id) VALUES ('{foreignTenant}');");
            var foreignTenantInsert = () => ExecuteAsync(database, InsertHandoffSql(Guid.NewGuid(), foreignTenant, assessment, project, estimate, budget, requisition, contract, profile, decision, costingWorkflow, contractorWorkflow));
            (await foreignTenantInsert.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(52220);

            await ExecuteAsync(database, $$"""
                INSERT dbo.CivilEngineeringMaintenanceCostingHandoffRevisions
                    (Id,HandoffId,Action,ToStage,ActorUserId,ActorName,CorrelationId,AfterJson,CreatedAt,TenantId,IsDeleted)
                VALUES ('{{revision}}','{{handoff}}',N'RefreshCivilCostingHandoff',N'Draft','{{actor}}',N'HOD',N'civil-costing-sql',N'{}',SYSUTCDATETIME(),'{{tenant}}',0);
                """);
            var mutateRevision = () => ExecuteAsync(database, $"UPDATE dbo.CivilEngineeringMaintenanceCostingHandoffRevisions SET Action=N'changed' WHERE Id='{revision}';");
            (await mutateRevision.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(52227);
        }
        finally
        {
            await ExecuteAsync(master, $"IF DB_ID(N'{databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]; END;");
        }
    }

    private static IReadOnlyList<SqlOperation> Operations()
    {
        var migration = new AddCivilEngineeringMaintenanceCostingHandoffs();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(migration, [builder]);
        return builder.Operations.OfType<SqlOperation>().ToList();
    }

    private static async Task SeedAsync(
        SqlConnection connection,
        Guid tenant,
        Guid actor,
        Guid intake,
        Guid assessment,
        Guid project,
        Guid estimate,
        Guid secondEstimate,
        Guid budget,
        Guid requisition,
        Guid contract,
        Guid profile,
        Guid decision,
        Guid costingWorkflow,
        Guid contractorWorkflow) =>
        await ExecuteAsync(connection, $$"""
            INSERT dbo.Tenants(Id) VALUES ('{{tenant}}');
            INSERT dbo.Users(Id,TenantId,IsActive) VALUES ('{{actor}}','{{tenant}}',1);
            INSERT dbo.Projects(Id,TenantId,IsDeleted,ContractId) VALUES ('{{project}}','{{tenant}}',0,'{{contract}}');
            INSERT dbo.CivilEngineeringMaintenanceIntakes(Id,TenantId,IsDeleted,ProjectId) VALUES ('{{intake}}','{{tenant}}',0,'{{project}}');
            INSERT dbo.CivilEngineeringMaintenanceAssessments(Id,IntakeId,TenantId,IsDeleted,Stage,Status) VALUES ('{{assessment}}','{{intake}}','{{tenant}}',0,N'Approved',N'Approved');
            INSERT dbo.QuantitySurveyEstimateVersions(Id,TenantId,ProjectId,IsDeleted,Status) VALUES ('{{estimate}}','{{tenant}}','{{project}}',0,N'Approved'),('{{secondEstimate}}','{{tenant}}','{{project}}',0,N'Approved');
            INSERT dbo.ProjectBudgetRevisions(Id,TenantId,ProjectId,IsDeleted) VALUES ('{{budget}}','{{tenant}}','{{project}}',0);
            INSERT dbo.PurchaseRequisitions(Id,TenantId,ProjectId,IsDeleted) VALUES ('{{requisition}}','{{tenant}}','{{project}}',0);
            INSERT dbo.Contracts(Id,TenantId,IsDeleted,Status) VALUES ('{{contract}}','{{tenant}}',0,N'Active');
            INSERT dbo.CivilEngineeringConfigurationProfiles(Id,TenantId,IsDeleted,LifecycleStatus,EffectiveFrom,EffectiveTo) VALUES ('{{profile}}','{{tenant}}',0,1,DATEADD(day,-1,SYSUTCDATETIME()),NULL);
            INSERT dbo.CivilEngineeringConfigurationDecisions(Id,TenantId,ProfileId,ConfigurationKey,IsDeleted,Status,ApprovalStatus,EvidenceStatus) VALUES ('{{decision}}','{{tenant}}','{{profile}}',N'CIV-CFG-008',0,2,1,2);
            INSERT dbo.WorkflowDefinitions(Id,TenantId,IsDeleted,IsActive,LifecycleStatus) VALUES ('{{costingWorkflow}}','{{tenant}}',0,1,1),('{{contractorWorkflow}}','{{tenant}}',0,1,1);
            """);

    private static string InsertHandoffSql(
        Guid handoff,
        Guid tenant,
        Guid assessment,
        Guid project,
        Guid estimate,
        Guid budget,
        Guid requisition,
        Guid contract,
        Guid profile,
        Guid decision,
        Guid costingWorkflow,
        Guid contractorWorkflow) =>
        $$"""
          INSERT dbo.CivilEngineeringMaintenanceCostingHandoffs
              (Id,AssessmentId,ProjectId,QuantitySurveyEstimateVersionId,ProjectBudgetRevisionId,PurchaseRequisitionId,ContractId,
               ConfigurationProfileId,ConfigurationDecisionId,CostingWorkflowDefinitionId,ContractorEngagementWorkflowDefinitionId,
               PolicyHash,Stage,Status,ApprovalStatus,ClientRequestId,RequestHash,CorrelationId,CreatedAt,TenantId,IsDeleted)
          VALUES
              ('{{handoff}}','{{assessment}}','{{project}}','{{estimate}}','{{budget}}','{{requisition}}','{{contract}}',
               '{{profile}}','{{decision}}','{{costingWorkflow}}','{{contractorWorkflow}}',
               REPLICATE('A',64),N'Draft',N'Draft',N'Draft',NEWID(),REPLICATE('B',64),N'civil-costing-sql',SYSUTCDATETIME(),'{{tenant}}',0);
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
                Skip = "Set RHEMA_TEST_SQLSERVER to run the disposable Civil costing-handoff SQL Server gate.";
        }
    }

    private const string MinimalSchemaSql = """
        CREATE TABLE dbo.Tenants (Id uniqueidentifier NOT NULL PRIMARY KEY);
        CREATE TABLE dbo.Users (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsActive bit NOT NULL);
        CREATE TABLE dbo.Projects (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL, ContractId uniqueidentifier NULL);
        CREATE TABLE dbo.CivilEngineeringMaintenanceIntakes (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL, ProjectId uniqueidentifier NULL);
        CREATE TABLE dbo.CivilEngineeringMaintenanceAssessments (Id uniqueidentifier NOT NULL PRIMARY KEY, IntakeId uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL, Stage nvarchar(40) NOT NULL, Status nvarchar(40) NOT NULL);
        CREATE TABLE dbo.QuantitySurveyEstimateVersions (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, ProjectId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL, Status nvarchar(40) NOT NULL);
        CREATE TABLE dbo.ProjectBudgetRevisions (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, ProjectId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL);
        CREATE TABLE dbo.PurchaseRequisitions (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, ProjectId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL);
        CREATE TABLE dbo.Contracts (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL, Status nvarchar(40) NOT NULL);
        CREATE TABLE dbo.CivilEngineeringConfigurationProfiles (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL, LifecycleStatus int NOT NULL, EffectiveFrom datetime2 NOT NULL, EffectiveTo datetime2 NULL);
        CREATE TABLE dbo.CivilEngineeringConfigurationDecisions (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, ProfileId uniqueidentifier NOT NULL, ConfigurationKey nvarchar(100) NOT NULL, IsDeleted bit NOT NULL, Status int NOT NULL, ApprovalStatus int NOT NULL, EvidenceStatus int NOT NULL);
        CREATE TABLE dbo.WorkflowDefinitions (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL, IsActive bit NOT NULL, LifecycleStatus int NOT NULL);
        """;
}
